using ModelOps.Domain;

namespace ModelOps.Api;

public sealed class ValidationWorker(
    IValidationQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ValidationWorker> logger) : BackgroundService
{
    private const int BatchSize = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var runId = await queue.DequeueAsync(stoppingToken);
            await ProcessRunAsync(runId, stoppingToken);
        }
    }

    private async Task ProcessRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ModelOpsDbContext>();
            var evaluator = scope.ServiceProvider.GetRequiredService<IElementRuleEvaluator>();
            var run = await db.ValidationRuns.SingleAsync(x => x.Id == runId, cancellationToken);

            if (run.Status != ValidationRunStatus.Pending)
            {
                logger.LogWarning("Validation run {RunId} was already started", runId);
                return;
            }

            run.Status = ValidationRunStatus.Running;
            run.StartedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            var processed = 0;
            while (processed < run.TotalElements)
            {
                var elements = await db.Elements
                    .AsNoTracking()
                    .Where(x => x.ProjectId == run.ProjectId)
                    .OrderBy(x => x.Id)
                    .Skip(processed)
                    .Take(BatchSize)
                    .Select(x => new ElementSnapshot(
                        x.Id,
                        x.ExternalId,
                        x.Category,
                        x.Name,
                        x.X,
                        x.Y,
                        x.Z))
                    .ToListAsync(cancellationToken);

                if (elements.Count == 0)
                {
                    break;
                }

                var issues = elements
                    .SelectMany(evaluator.Evaluate)
                    .Select(finding => new ValidationIssue
                    {
                        RunId = run.Id,
                        ElementId = finding.ElementId,
                        RuleCode = finding.RuleCode,
                        Severity = finding.Severity,
                        Message = finding.Message
                    })
                    .ToList();

                db.ValidationIssues.AddRange(issues);
                processed += elements.Count;
                run.ProcessedElements = processed;
                run.IssueCount += issues.Count;
                await db.SaveChangesAsync(cancellationToken);

                foreach (var issue in issues)
                {
                    db.Entry(issue).State = EntityState.Detached;
                }
            }

            run.Status = ValidationRunStatus.Completed;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Validation run {RunId} completed: {Elements} elements, {Issues} issues",
                run.Id,
                run.ProcessedElements,
                run.IssueCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Validation run {RunId} failed", runId);
            await MarkAsFailedAsync(runId, exception.Message, cancellationToken);
        }
    }

    private async Task MarkAsFailedAsync(
        Guid runId,
        string error,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ModelOpsDbContext>();
        var run = await db.ValidationRuns.SingleOrDefaultAsync(x => x.Id == runId, cancellationToken);
        if (run is null)
        {
            return;
        }

        run.Status = ValidationRunStatus.Failed;
        run.Error = error.Length <= 2_000 ? error : error[..2_000];
        run.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}

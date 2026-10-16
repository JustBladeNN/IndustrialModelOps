using Microsoft.AspNetCore.Http.HttpResults;
using ModelOps.Domain;

namespace ModelOps.Api;

internal static class Endpoints
{
    public static IEndpointRouteBuilder MapModelOpsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", HealthAsync).WithTags("System");

        var projects = app.MapGroup("/api/v1/projects").WithTags("Projects");
        projects.MapGet("/", GetProjectsAsync);
        projects.MapPost("/", CreateProjectAsync);
        projects.MapPost("/{projectId:guid}/generate", GenerateElementsAsync);
        projects.MapGet("/{projectId:guid}/elements", GetElementsAsync);
        projects.MapPost("/{projectId:guid}/checks", StartCheckAsync);

        var checks = app.MapGroup("/api/v1/checks").WithTags("Checks");
        checks.MapGet("/{runId:guid}", GetRunAsync);
        checks.MapGet("/{runId:guid}/issues", GetIssuesAsync);

        app.MapPatch("/api/v1/issues/{issueId:guid}", ResolveIssueAsync)
            .WithTags("Issues");

        return app;
    }

    private static async Task<IResult> HealthAsync(
        ModelOpsDbContext db,
        CancellationToken cancellationToken)
    {
        var databaseAvailable = await db.Database.CanConnectAsync(cancellationToken);
        return databaseAvailable
            ? Results.Ok(new { status = "Healthy", database = "PostgreSQL" })
            : Results.Problem("PostgreSQL is unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<Ok<List<ProjectResponse>>> GetProjectsAsync(
        ModelOpsDbContext db,
        CancellationToken cancellationToken)
    {
        var projects = await db.Projects
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ProjectResponse(x.Id, x.Name, x.CreatedAt, x.Elements.Count))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(projects);
    }

    private static async Task<Results<Created<ProjectResponse>, ValidationProblem>> CreateProjectAsync(
        CreateProjectRequest request,
        ModelOpsDbContext db,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length is < 3 or > 200)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Name)] = ["Name must contain between 3 and 200 characters."]
            });
        }

        var project = new ModelProject { Name = name };
        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);

        var response = new ProjectResponse(project.Id, project.Name, project.CreatedAt, 0);
        return TypedResults.Created($"/api/v1/projects/{project.Id}", response);
    }

    private static async Task<IResult> GenerateElementsAsync(
        Guid projectId,
        GenerateElementsRequest request,
        ModelOpsDbContext db,
        CancellationToken cancellationToken)
    {
        if (request.Count is < 1 or > 50_000)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Count)] = ["Count must be between 1 and 50000."]
            });
        }

        if (!await db.Projects.AnyAsync(x => x.Id == projectId, cancellationToken))
        {
            return Results.NotFound();
        }

        var random = new Random(42);
        var categories = new[] { "Pipe", "Valve", "Equipment", "Cable", "Structure" };
        const int batchSize = 1_000;

        for (var offset = 0; offset < request.Count; offset += batchSize)
        {
            var size = Math.Min(batchSize, request.Count - offset);
            var batch = new List<ModelElement>(size);

            for (var index = offset; index < offset + size; index++)
            {
                var number = index + 1;
                batch.Add(new ModelElement
                {
                    ProjectId = projectId,
                    ExternalId = $"EL-{number:000000}",
                    Category = number % 53 == 0 ? "Unknown" : categories[number % categories.Length],
                    Name = number % 37 == 0 ? "" : $"Engineering element {number}",
                    X = number % 97 == 0 ? 20_000 : random.NextDouble() * 1_000,
                    Y = random.NextDouble() * 1_000,
                    Z = random.NextDouble() * 100,
                    ParametersJson = $"{{\"material\":\"M{number % 12}\",\"system\":\"S{number % 8}\"}}"
                });
            }

            db.Elements.AddRange(batch);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        return Results.Ok(new { projectId, generated = request.Count });
    }

    private static async Task<IResult> GetElementsAsync(
        Guid projectId,
        ModelOpsDbContext db,
        string? search = null,
        string? category = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.Elements.AsNoTracking().Where(x => x.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Name, pattern) ||
                                     EF.Functions.ILike(x.ExternalId, pattern));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.Category == category);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.ExternalId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ElementResponse(
                x.Id,
                x.ExternalId,
                x.Category,
                x.Name,
                x.X,
                x.Y,
                x.Z))
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedResponse<ElementResponse>(items, page, pageSize, totalCount));
    }

    private static async Task<IResult> StartCheckAsync(
        Guid projectId,
        ModelOpsDbContext db,
        IValidationQueue queue,
        CancellationToken cancellationToken)
    {
        if (!await db.Projects.AnyAsync(x => x.Id == projectId, cancellationToken))
        {
            return Results.NotFound();
        }

        var count = await db.Elements.CountAsync(x => x.ProjectId == projectId, cancellationToken);
        if (count == 0)
        {
            return Results.Conflict(new { error = "Generate model elements before starting a check." });
        }

        var run = new ValidationRun
        {
            ProjectId = projectId,
            TotalElements = count
        };

        db.ValidationRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        await queue.EnqueueAsync(run.Id, cancellationToken);

        return Results.Accepted($"/api/v1/checks/{run.Id}", ToResponse(run));
    }

    private static async Task<IResult> GetRunAsync(
        Guid runId,
        ModelOpsDbContext db,
        CancellationToken cancellationToken)
    {
        var run = await db.ValidationRuns.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == runId, cancellationToken);
        return run is null ? Results.NotFound() : Results.Ok(ToResponse(run));
    }

    private static async Task<IResult> GetIssuesAsync(
        Guid runId,
        ModelOpsDbContext db,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = db.ValidationIssues.AsNoTracking().Where(x => x.RunId == runId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.Severity)
            .ThenBy(x => x.RuleCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new IssueResponse(
                x.Id,
                x.ElementId,
                x.RuleCode,
                x.Severity,
                x.Message,
                x.IsResolved,
                x.Version))
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedResponse<IssueResponse>(items, page, pageSize, totalCount));
    }

    private static async Task<IResult> ResolveIssueAsync(
        Guid issueId,
        ResolveIssueRequest request,
        ModelOpsDbContext db,
        CancellationToken cancellationToken)
    {
        var issue = await db.ValidationIssues.SingleOrDefaultAsync(x => x.Id == issueId, cancellationToken);
        if (issue is null)
        {
            return Results.NotFound();
        }

        if (issue.Version != request.ExpectedVersion)
        {
            return Results.Conflict(new
            {
                error = "The issue was changed by another user.",
                currentVersion = issue.Version
            });
        }

        issue.IsResolved = request.IsResolved;
        issue.Version = Guid.NewGuid();

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            var currentVersion = await db.ValidationIssues
                .Where(x => x.Id == issueId)
                .Select(x => (Guid?)x.Version)
                .SingleOrDefaultAsync(cancellationToken);

            return Results.Conflict(new
            {
                error = "The issue was changed by another user.",
                currentVersion
            });
        }

        return Results.Ok(new IssueResponse(
            issue.Id,
            issue.ElementId,
            issue.RuleCode,
            issue.Severity,
            issue.Message,
            issue.IsResolved,
            issue.Version));
    }

    private static ValidationRunResponse ToResponse(ValidationRun run) => new(
        run.Id,
        run.ProjectId,
        run.Status,
        run.TotalElements,
        run.ProcessedElements,
        run.IssueCount,
        run.CreatedAt,
        run.StartedAt,
        run.CompletedAt,
        run.Error);
}

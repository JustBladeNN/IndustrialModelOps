using ModelOps.Domain;

namespace ModelOps.Application;

public sealed record CreateProjectRequest(string Name);
public sealed record GenerateElementsRequest(int Count = 10_000);

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    int ElementCount);

public sealed record ElementResponse(
    Guid Id,
    string ExternalId,
    string Category,
    string Name,
    double X,
    double Y,
    double Z);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ValidationRunResponse(
    Guid Id,
    Guid ProjectId,
    ValidationRunStatus Status,
    int TotalElements,
    int ProcessedElements,
    int IssueCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error)
{
    public int ProgressPercent => TotalElements == 0
        ? 0
        : (int)Math.Round(ProcessedElements * 100d / TotalElements);
}

public sealed record IssueResponse(
    Guid Id,
    Guid ElementId,
    string RuleCode,
    IssueSeverity Severity,
    string Message,
    bool IsResolved,
    Guid Version);

public sealed record ResolveIssueRequest(bool IsResolved, Guid ExpectedVersion);

public sealed record ElementSnapshot(
    Guid Id,
    string ExternalId,
    string Category,
    string Name,
    double X,
    double Y,
    double Z);

public sealed record ValidationFinding(
    Guid ElementId,
    string RuleCode,
    IssueSeverity Severity,
    string Message);

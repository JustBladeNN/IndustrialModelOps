namespace ModelOps.Domain;

public sealed class ModelProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ModelElement> Elements { get; set; } = [];
}

public sealed class ModelElement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public ModelProject? Project { get; set; }
    public required string ExternalId { get; set; }
    public required string Category { get; set; }
    public required string Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public string ParametersJson { get; set; } = "{}";
}

public sealed class ValidationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public ValidationRunStatus Status { get; set; } = ValidationRunStatus.Pending;
    public int TotalElements { get; set; }
    public int ProcessedElements { get; set; }
    public int IssueCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Error { get; set; }
    public List<ValidationIssue> Issues { get; set; } = [];
}

public sealed class ValidationIssue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public ValidationRun? Run { get; set; }
    public Guid ElementId { get; set; }
    public required string RuleCode { get; set; }
    public IssueSeverity Severity { get; set; }
    public required string Message { get; set; }
    public bool IsResolved { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}

public enum ValidationRunStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

public enum IssueSeverity
{
    Info,
    Warning,
    Error
}

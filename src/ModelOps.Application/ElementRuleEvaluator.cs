using ModelOps.Domain;

namespace ModelOps.Application;

public interface IElementRuleEvaluator
{
    IReadOnlyList<ValidationFinding> Evaluate(ElementSnapshot element);
}

public sealed class ElementRuleEvaluator : IElementRuleEvaluator
{
    private const double CoordinateLimit = 10_000;

    public IReadOnlyList<ValidationFinding> Evaluate(ElementSnapshot element)
    {
        var findings = new List<ValidationFinding>(3);

        if (string.IsNullOrWhiteSpace(element.Name))
        {
            findings.Add(new(
                element.Id,
                "MISSING_NAME",
                IssueSeverity.Error,
                $"Element {element.ExternalId} has no display name."));
        }

        if (string.Equals(element.Category, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new(
                element.Id,
                "UNKNOWN_CATEGORY",
                IssueSeverity.Warning,
                $"Element {element.ExternalId} has an unknown category."));
        }

        if (Math.Abs(element.X) > CoordinateLimit ||
            Math.Abs(element.Y) > CoordinateLimit ||
            Math.Abs(element.Z) > CoordinateLimit)
        {
            findings.Add(new(
                element.Id,
                "OUT_OF_BOUNDS",
                IssueSeverity.Error,
                $"Element {element.ExternalId} is outside the allowed coordinate range."));
        }

        return findings;
    }
}

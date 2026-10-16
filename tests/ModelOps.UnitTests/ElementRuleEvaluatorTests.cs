using ModelOps.Application;

namespace ModelOps.UnitTests;

public sealed class ElementRuleEvaluatorTests
{
    private readonly ElementRuleEvaluator _sut = new();

    [Fact]
    public void Evaluate_HealthyElement_ReturnsNoFindings()
    {
        var element = new ElementSnapshot(Guid.NewGuid(), "EL-1", "Pipe", "Supply pipe", 1, 2, 3);

        var findings = _sut.Evaluate(element);

        Assert.Empty(findings);
    }

    [Fact]
    public void Evaluate_InvalidElement_ReturnsAllMatchingFindings()
    {
        var element = new ElementSnapshot(Guid.NewGuid(), "EL-2", "Unknown", "", 20_000, 0, 0);

        var findings = _sut.Evaluate(element);

        Assert.Collection(
            findings,
            item => Assert.Equal("MISSING_NAME", item.RuleCode),
            item => Assert.Equal("UNKNOWN_CATEGORY", item.RuleCode),
            item => Assert.Equal("OUT_OF_BOUNDS", item.RuleCode));
    }
}

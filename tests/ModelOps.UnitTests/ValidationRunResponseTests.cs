namespace ModelOps.UnitTests;

public sealed class ValidationRunResponseTests
{
    [Fact]
    public void ProgressPercent_CalculatesRoundedProgress()
    {
        var response = new ModelOps.Application.ValidationRunResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ModelOps.Domain.ValidationRunStatus.Running,
            300,
            200,
            5,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            null);

        Assert.Equal(67, response.ProgressPercent);
    }
}

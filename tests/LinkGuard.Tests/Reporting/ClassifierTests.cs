using LinkGuard.Reporting;

namespace LinkGuard.Tests.Reporting;

public class ClassifierTests
{
    [Theory]
    [InlineData(200, true, CheckOutcome.Pass)]
    [InlineData(200, false, CheckOutcome.Pass)]
    [InlineData(404, true, CheckOutcome.Fail)]
    [InlineData(404, false, CheckOutcome.Warn)]
    [InlineData(500, true, CheckOutcome.Fail)]
    [InlineData(500, false, CheckOutcome.Warn)]
    public void Classify_StatusAndScope_ProducesOutcome(int status, bool isInternal, CheckOutcome expected)
    {
        var kind = isInternal ? LinkKind.Internal : LinkKind.External;

        Assert.Equal(expected, Classifier.Classify(status, error: null, kind));
    }

    [Theory]
    [InlineData(true, CheckOutcome.Pass)]
    [InlineData(false, CheckOutcome.Pass)]
    public void Classify_RedirectResolvingTo2xx_ProducesPass(bool isInternal, CheckOutcome expected)
    {
        var kind = isInternal ? LinkKind.Internal : LinkKind.External;

        // The redirect chain itself is not passed to Classify - only the final status matters.
        Assert.Equal(expected, Classifier.Classify(200, error: null, kind));
    }

    [Theory]
    [InlineData(true, CheckOutcome.Fail)]
    [InlineData(false, CheckOutcome.Warn)]
    public void Classify_RedirectResolvingTo4xx_ProducesFailOrWarn(bool isInternal, CheckOutcome expected)
    {
        var kind = isInternal ? LinkKind.Internal : LinkKind.External;

        Assert.Equal(expected, Classifier.Classify(404, error: null, kind));
    }

    [Theory]
    [InlineData(true, CheckOutcome.Fail)]
    [InlineData(false, CheckOutcome.Warn)]
    public void Classify_TimeoutAfterRetries_ProducesFailOrWarn(bool isInternal, CheckOutcome expected)
    {
        var kind = isInternal ? LinkKind.Internal : LinkKind.External;

        Assert.Equal(expected, Classifier.Classify(null, "timeout", kind));
    }

    [Theory]
    [InlineData("dns failure", true, CheckOutcome.Fail)]
    [InlineData("dns failure", false, CheckOutcome.Warn)]
    [InlineData("tls failure", true, CheckOutcome.Fail)]
    [InlineData("tls failure", false, CheckOutcome.Warn)]
    public void Classify_TransportFailure_ProducesFailOrWarn(string error, bool isInternal, CheckOutcome expected)
    {
        var kind = isInternal ? LinkKind.Internal : LinkKind.External;

        Assert.Equal(expected, Classifier.Classify(null, error, kind));
    }

    [Fact]
    public void Classify_NoExternalOnlyFailureEverProducesFail()
    {
        // The single most important assertion in the suite: if this regresses, the tool
        // starts blocking releases for reasons outside anyone's control.
        int?[] statuses = [null, 200, 301, 404, 500, 503];
        string?[] errors = [null, "timeout", "dns failure", "tls failure", "redirect loop", "too many redirects"];

        foreach (var status in statuses)
        foreach (var error in errors)
        {
            Assert.NotEqual(CheckOutcome.Fail, Classifier.Classify(status, error, LinkKind.External));
        }
    }
}

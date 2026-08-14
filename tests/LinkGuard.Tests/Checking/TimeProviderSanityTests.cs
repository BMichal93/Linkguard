using Microsoft.Extensions.Time.Testing;

namespace LinkGuard.Tests.Checking;

public class TimeProviderSanityTests
{
    [Fact]
    public async Task FakeTimeProvider_AdvancingPastDelay_CompletesTheTask()
    {
        var timeProvider = new FakeTimeProvider();
        var task = Task.Delay(TimeSpan.FromSeconds(1), timeProvider);

        Assert.False(task.IsCompleted);

        timeProvider.Advance(TimeSpan.FromSeconds(2));

        await task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(task.IsCompleted);
    }
}

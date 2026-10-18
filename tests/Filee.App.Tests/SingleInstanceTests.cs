// A later launch hands its arguments to the running Filee, also while that one is still starting.

using Filee.App.Services;

namespace Filee.App.Tests;

public class SingleInstanceTests
{
    [Fact]
    public async Task Arguments_reach_a_first_instance_that_starts_listening_late()
    {
        // The installer's --quit (or a second launch) came 8 s after Filee started, on a computer where Filee took 12 s
        // to start listening: one 3-second try gave up and the request was lost.
        var name = "Filee-test-" + Guid.NewGuid().ToString("N")[..8];
        using var first = SingleInstance.Acquire(name);
        Assert.True(first.IsFirst);
        var received = new TaskCompletionSource<string[]>();
        first.ArgumentsReceived += args => received.TrySetResult(args);

        var forward = Task.Run(() =>
        {
            using var second = SingleInstance.Acquire(name);
            Assert.False(second.IsFirst);
            return second.ForwardToFirst(["--quit"], TimeSpan.FromSeconds(15));
        }, TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken); // longer than the old single try
        first.StartListening();

        Assert.True(await forward.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
        Assert.Equal(["--quit"], await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_copy_with_its_own_data_folder_never_reaches_the_users_filee()
    {
        // The name without a data folder must stay the same: a new version forwards to an older one that runs.
        var usual = "Filee-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("someone"u8.ToArray()))[..12];
        Assert.Equal(usual, SingleInstance.NameFor("someone", null));
        var test = SingleInstance.NameFor("someone", Path.Combine(Path.GetTempPath(), "filee-a"));
        Assert.NotEqual(usual, test);
        Assert.NotEqual(test, SingleInstance.NameFor("someone", Path.Combine(Path.GetTempPath(), "filee-b")));
        Assert.Equal(test, SingleInstance.NameFor("someone", Path.Combine(Path.GetTempPath(), "filee-a")));
    }

    [Fact]
    public async Task Forwarding_gives_up_when_nobody_listens()
    {
        var name = "Filee-test-" + Guid.NewGuid().ToString("N")[..8];
        using var first = SingleInstance.Acquire(name);

        var delivered = await Task.Run(() =>
        {
            using var instance = SingleInstance.Acquire(name);
            return instance.ForwardToFirst(["--quit"], TimeSpan.FromSeconds(1));
        }, TestContext.Current.CancellationToken);

        Assert.False(delivered);
    }
}

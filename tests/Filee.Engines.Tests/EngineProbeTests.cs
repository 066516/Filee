// Engine checks that must not hang: a network share on the PATH that doesn't answer, an FFmpeg that never exits.

using System.Diagnostics;
using Filee.Engines.Infrastructure;
using Filee.Engines.Media;

namespace Filee.Engines.Tests;

public class EngineProbeTests
{
    [Theory]
    [InlineData(@"\\server\share\tools\bin", @"\\server\share")]
    [InlineData(@"\\server\share", @"\\server\share")]
    [InlineData(@"\\server\share\", @"\\server\share")]
    [InlineData(@"\\server", @"\\server")]
    [InlineData(@"C:\Tools\bin", null)]
    [InlineData(@"\\?\C:\Tools", null)]
    [InlineData(@"\\.\pipe\x", null)]
    [InlineData("/usr/local/bin", null)]
    [InlineData("", null)]
    public void Network_folders_on_the_path_are_recognised(string folder, string? share) =>
        Assert.Equal(share, EngineEnvironment.ShareOf(folder));

    [Fact]
    public void A_program_that_never_exits_is_given_up_on_and_killed()
    {
        // Both run for about half a minute (ping waits a second between replies).
        using var process = OperatingSystem.IsWindows() ? Start("ping.exe", "-n 30 127.0.0.1") : Start("/bin/sleep", "30");
        var watch = Stopwatch.StartNew();

        Assert.Null(FfmpegConverter.OutputWithin(process, TimeSpan.FromSeconds(1)));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"took {watch.Elapsed}");
        Assert.True(process.WaitForExit(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void Output_of_a_program_that_exits_is_returned()
    {
        using var process = OperatingSystem.IsWindows()
            ? Start("cmd.exe", "/d /c echo filee-probe")
            : Start("/bin/sh", "-c \"echo filee-probe\"");

        Assert.Contains("filee-probe", FfmpegConverter.OutputWithin(process, TimeSpan.FromSeconds(10)));
        Assert.Equal(0, process.ExitCode);
    }

    private static Process Start(string program, string arguments) =>
        Process.Start(new ProcessStartInfo(program, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException(program + " did not start");
}

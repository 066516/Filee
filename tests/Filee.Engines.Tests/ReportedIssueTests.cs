// End-to-end regressions from the public issue audit: real converters and files, checked after conversion.

using Filee.Core.Conversion;
using Filee.Core.Formats;
using Filee.Core.Presets;
using Filee.Core.Watching;
using Filee.Engines.Infrastructure;
using Filee.Engines.Media;
using ImageMagick;

namespace Filee.Engines.Tests;

public class ReportedIssueTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    [Theory]
    [InlineData("mp4")]
    [InlineData("mkv")]
    public async Task Issue292_video_cd_dat_converts_with_video_and_audio_that_decode(string target)
    {
        var tools = FfmpegConverter.Locate();
        Assert.SkipUnless(tools is not null, "FFmpeg is needed for the Video CD integration test.");
        var folder = fx.NewFolder();
        var input = Path.Combine(folder, "AVSEQ01.DAT");
        var create = await ProcessRunner.RunAsync(tools.Value.Ffmpeg,
            ["-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc2=size=352x288:rate=25",
             "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100", "-t", "0.4",
             "-target", "pal-vcd", "-f", "mpeg", input], TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.True(create.ExitCode == 0, create.StandardError);

        var job = await fx.ConvertAsync([input], new Preset { TargetFormat = target });
        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => f.ErrorDetail)));
        var output = Assert.Single(job.Outputs);
        var decode = await ProcessRunner.RunAsync(tools.Value.Ffmpeg,
            ["-v", "error", "-i", output, "-map", "0:v:0", "-map", "0:a:0", "-f", "null", "-"],
            TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.True(decode.ExitCode == 0, decode.StandardError);
        Assert.True(File.Exists(input));
    }

    [Fact]
    public async Task Issue292_non_video_dat_is_not_reported_as_a_successful_video()
    {
        Assert.SkipUnless(FfmpegConverter.Locate() is not null, "FFmpeg is needed for the DAT validation test.");
        var input = Path.Combine(fx.NewFolder(), "database.dat");
        await File.WriteAllTextAsync(input, "This is application data, not a video.", TestContext.Current.CancellationToken);
        var job = await fx.ConvertAsync([input], new Preset { TargetFormat = "mp4" });
        Assert.Equal(JobState.Failed, job.State);
        Assert.Empty(job.Outputs);
        Assert.True(File.Exists(input));
    }

    [Theory]
    [InlineData("py")]
    [InlineData("log")]
    [InlineData("pac")]
    [InlineData("bat")]
    [InlineData("command")]
    public async Task Reported_plain_text_extensions_convert_to_txt_without_executing_or_losing_content(string extension)
    {
        var folder = fx.NewFolder();
        var input = Path.Combine(folder, "source." + extension);
        const string Content = "# Local source text\nprint('مرحبا 中文 שלום')\n<not markup> & \"quoted\"\n";
        await File.WriteAllTextAsync(input, Content, TestContext.Current.CancellationToken);

        var job = await fx.ConvertAsync([input], new Preset { TargetFormat = "txt" });

        Assert.Equal(JobState.Completed, job.State);
        var output = Assert.Single(job.Outputs);
        Assert.Equal(".txt", Path.GetExtension(output));
        Assert.Equal(Content, await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken));
        Assert.Equal(Content, await File.ReadAllTextAsync(input, TestContext.Current.CancellationToken));
        Assert.Equal("txt", FormatRegistry.Detect(input)?.Id);
    }

    [Theory]
    [InlineData("py")]
    [InlineData("bat")]
    [InlineData("command")]
    public async Task Exporting_script_source_never_executes_the_script(string extension)
    {
        var folder = fx.NewFolder();
        var marker = Path.Combine(folder, "executed.txt").Replace('\\', '/');
        var input = Path.Combine(folder, "script." + extension);
        var content = extension switch
        {
            "py" => $"from pathlib import Path\nPath(r'{marker}').write_text('executed')\n",
            "bat" => $"@echo off\r\necho executed>\"{marker}\"\r\n",
            _ => $"#!/bin/sh\nprintf executed > '{marker}'\n",
        };
        await File.WriteAllTextAsync(input, content, TestContext.Current.CancellationToken);
        var job = await fx.ConvertAsync([input], new Preset { TargetFormat = "txt" });
        Assert.Equal(JobState.Completed, job.State);
        Assert.Equal(content, await File.ReadAllTextAsync(Assert.Single(job.Outputs), TestContext.Current.CancellationToken));
        Assert.False(File.Exists(marker));
    }

    [Theory]
    [InlineData(AfterConversion.Keep)]
    [InlineData(AfterConversion.MoveToOriginals)]
    public async Task Issue234_webp_becomes_a_readable_jpg_in_the_same_watch_folder(AfterConversion originals)
    {
        var folder = fx.NewFolder();
        var input = EngineFixture.MakeImage(folder, "photo.webp", MagickFormat.WebP);
        var rule = new WatchRule { Folder = folder, OutputFolder = folder, Originals = originals };
        var jobs = new List<ConversionJob>();
        await using var watcher = new FolderWatcher(rule, async (files, _) =>
        {
            var job = await fx.ConvertAsync(files, rule.Apply(new Preset { TargetFormat = "jpg" }));
            jobs.Add(job);
            return job;
        }, settleTime: TimeSpan.Zero);

        watcher.Notice(input);
        Assert.Equal(1, await watcher.ProcessPendingAsync());
        var job = Assert.Single(jobs);
        Assert.Equal(JobState.Completed, job.State);
        var output = Assert.Single(job.Outputs);
        Assert.Equal(Path.Combine(folder, "photo.jpg"), output);
        using var image = new MagickImage(output);
        Assert.Equal(MagickFormat.Jpeg, image.Format);
        Assert.Equal(320u, image.Width);
        Assert.Equal(200u, image.Height);
        Assert.Equal(originals == AfterConversion.Keep, File.Exists(input));
        if (originals == AfterConversion.MoveToOriginals)
            Assert.True(File.Exists(Path.Combine(rule.OriginalsFolder, "photo.webp")));

        watcher.Notice(output);
        Assert.Equal(0, await watcher.ProcessPendingAsync());
        Assert.Single(jobs);
    }
}

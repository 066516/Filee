// "Replace the original" (#332): the finished result takes the source's place and the source goes to the Trash, or
// both are kept with a note saying why.

using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Core.Watching;

namespace Filee.Core.Tests;

public class ReplaceOriginalTests
{
    private static readonly Preset Replacing = new() { TargetFormat = BuiltInData.SameAsSource, Output = { ReplaceOriginal = true } };

    /// <summary>Moves files into a folder, or refuses with <paramref name="refuse"/>.</summary>
    private sealed class FakeTrash(string folder, string? refuse = null)
    {
        public List<string> Moved { get; } = [];

        public string? Move(string path)
        {
            if (refuse is not null)
                return refuse;
            File.Move(path, Path.Combine(folder, Path.GetFileName(path)));
            Moved.Add(path);
            return null;
        }
    }

    private static async Task<ConversionJob> Run(FakeTrash trash, string source, Preset preset, ISourceFetcher? fetcher = null)
    {
        await using var queue = new JobQueue(new ConverterCatalog([new FakeConverter("magick", new ConversionEdge("jpg", "jpg"), new ConversionEdge("jpg", "png"))]),
            moveToTrash: trash.Move);
        var job = new ConversionJob([source], preset, "Test", fetcher);
        await queue.RunAsync(job);
        return job;
    }

    [Fact]
    public async Task The_result_takes_the_place_of_the_original_which_goes_to_the_trash()
    {
        using var dir = new TempDir();
        using var bin = new TempDir();
        var source = dir.File("a.jpg");
        var trash = new FakeTrash(bin.Path);

        var job = await Run(trash, source, Replacing);

        Assert.Equal(JobState.Completed, job.State);
        Assert.Equal([source], trash.Moved);
        Assert.Equal([source], job.Outputs);
        Assert.Equal("a.jpg→jpg by magick", File.ReadAllText(source));
        Assert.Equal("x", File.ReadAllText(Path.Combine(bin.Path, "a.jpg")));
        Assert.Equal([source], Directory.GetFiles(dir.Path));
        Assert.Null(job.Files[0].NoticeKey);
    }

    [Fact]
    public async Task When_the_trash_refuses_both_are_kept_with_the_reason()
    {
        using var dir = new TempDir();
        var source = dir.File("a.jpg");

        var job = await Run(new FakeTrash(dir.Path, refuse: "the file is open in another program"), source, Replacing);

        Assert.Equal(JobState.Completed, job.State);
        Assert.Equal("x", File.ReadAllText(source));
        var output = Assert.Single(job.Outputs);
        Assert.NotEqual(source, output);
        Assert.True(File.Exists(output));
        Assert.Equal("notice.original_kept", job.Files[0].NoticeKey);
        Assert.Equal("the file is open in another program", job.Files[0].NoticeDetail);
    }

    [Fact]
    public async Task Without_a_trash_the_original_is_kept()
    {
        using var dir = new TempDir();
        var source = dir.File("a.jpg");
        await using var queue = new JobQueue(new ConverterCatalog([new FakeConverter("magick", new ConversionEdge("jpg", "jpg"))]));
        var job = new ConversionJob([source], Replacing, "Test");

        await queue.RunAsync(job);

        Assert.Equal("x", File.ReadAllText(source));
        Assert.Equal(2, Directory.GetFiles(dir.Path).Length);
        Assert.Equal("notice.original_kept", job.Files[0].NoticeKey);
    }

    [Fact]
    public async Task Another_format_is_saved_beside_the_original()
    {
        using var dir = new TempDir();
        using var bin = new TempDir();
        var source = dir.File("a.jpg");
        var trash = new FakeTrash(bin.Path);

        var job = await Run(trash, source, new Preset { TargetFormat = "png", Output = { ReplaceOriginal = true } });

        Assert.Empty(trash.Moved);
        Assert.Equal("x", File.ReadAllText(source));
        Assert.Equal([Path.Combine(dir.Path, "a.png")], job.Outputs);
        Assert.Null(job.Files[0].NoticeKey);
    }

    [Fact]
    public async Task Off_by_default()
    {
        using var dir = new TempDir();
        using var bin = new TempDir();
        var source = dir.File("a.jpg");
        var trash = new FakeTrash(bin.Path);

        await Run(trash, source, new Preset { TargetFormat = BuiltInData.SameAsSource });

        Assert.False(new OutputRule().ReplaceOriginal);
        Assert.Empty(trash.Moved);
        Assert.Equal("x", File.ReadAllText(source));
    }

    [Fact]
    public async Task Files_from_a_browser_are_never_replaced()
    {
        using var dir = new TempDir();
        using var bin = new TempDir();
        var source = dir.File("a.jpg");
        var trash = new FakeTrash(bin.Path);

        await Run(trash, source, Replacing, new LocalFetcher());

        Assert.Empty(trash.Moved);
        Assert.Equal("x", File.ReadAllText(source));
    }

    [Fact]
    public void Watch_folders_never_replace_what_they_watch()
    {
        var rule = new WatchRule { Folder = "in", OutputFolder = "out" };

        Assert.False(rule.Apply(Replacing).Output.ReplaceOriginal);
        Assert.True(Replacing.Output.ReplaceOriginal);
    }

    [Fact]
    public void Survives_a_preset_round_trip()
    {
        var copy = Replacing.Clone();

        Assert.True(copy.Output.ReplaceOriginal);
    }

    /// <summary>A browser drop whose files are already on disk.</summary>
    private sealed class LocalFetcher : ISourceFetcher
    {
        public bool Fetches(string source) => false;
        public Task<string> FetchAsync(string source, IProgress<TransferProgress> progress, CancellationToken cancellationToken) => Task.FromResult(source);
        public void Release(ConversionJob job) { }
    }
}

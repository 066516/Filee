// The update notice says how much an update matters and what it changes, from a hidden block in the release notes.

using System.Net;
using System.Text;
using System.Text.Json;
using Avalonia.Headless.XUnit;
using Filee.App.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Filee.App.Tests;

public class ReleaseNotesTests
{
    private const string Body = """
        ## What's Changed

        <!-- update
        level: required
        ar: تم نقل فواصل صفحات Excel إلى PDF
        ar: معادلات هانغل تظهر كما هي
        en: Excel page breaks carry over to PDF
        fr: Les sauts de page Excel passent dans le PDF
        -->

        ### Fixed
        """;

    [Fact]
    public void The_hidden_block_gives_the_level_and_a_line_per_change_by_language()
    {
        var note = ReleaseNotes.Parse("1.10.3", Body);

        Assert.Equal(UpdateLevel.Required, note.Level);
        Assert.Equal(["تم نقل فواصل صفحات Excel إلى PDF", "معادلات هانغل تظهر كما هي"], note.In("ar"));
        Assert.Equal(["Les sauts de page Excel passent dans le PDF"], note.In("fr"));
        Assert.Equal(["Excel page breaks carry over to PDF"], note.In("ko")); // not a language Takaconv ships: English
    }

    [Fact]
    public void Every_language_takaconv_ships_keeps_its_lines_and_the_others_read_english()
    {
        var note = ReleaseNotes.Parse("1.12.0", """
            <!-- update
            en: Arabic translation
            ar: الترجمة العربية
            fr: Traduction française
            pl: Język polski
            xx: from a newer Filee
            -->
            """);

        Assert.Equal(["الترجمة العربية"], note.In("ar"));
        Assert.Equal(["Traduction française"], note.In("fr"));
        Assert.Equal(["Arabic translation"], note.In("pl")); // no longer a shipped language: English
        Assert.Equal(["Arabic translation"], note.In("xx")); // not a Takaconv language: English
    }

    [Fact]
    public void Releases_without_the_block_are_recommended_and_say_nothing()
    {
        var note = ReleaseNotes.Parse("1.9.0", "### New\n\n- Something");

        Assert.Equal(UpdateLevel.Recommended, note.Level);
        Assert.Empty(note.In("ar"));
    }

    [Fact]
    public void Someone_a_few_versions_behind_gets_every_version_and_the_highest_level()
    {
        var notes = ReleaseNotes.Between(
        [
            ("v9.9.12", "<!-- update\nlevel: optional\n-->"),   // newer than the latest the check found: not yet
            ("v9.9.11", "<!-- update\nlevel: optional\nar: ميزة جديدة\n-->"),
            ("v9.9.10", "<!-- update\nlevel: required\nar: إصلاح مهم\n-->"),
            ("v9.9.9", "no block"),
            ("v1.0.0", "<!-- update\nlevel: required\n-->"),    // older than this version
        ], current: "9.9.9", latest: "9.9.11");

        Assert.Equal(["9.9.11", "9.9.10"], notes.Select(n => n.Version));
        Assert.Equal(UpdateLevel.Required, ReleaseNotes.Highest(notes));
        Assert.Equal(UpdateLevel.Optional, ReleaseNotes.Highest([notes[0]]));
        Assert.Equal(UpdateLevel.Recommended, ReleaseNotes.Highest([]));
    }

    [AvaloniaFact] // the check publishes its answer on the UI thread
    public async Task The_check_reads_the_notes_of_every_newer_release()
    {
        var updates = new UpdateService(NullLogger<UpdateService>.Instance, new Releases());

        Assert.True(await updates.RefreshAsync(announce: false));

        Assert.Equal("9.9.11", updates.LatestVersion);
        Assert.Equal(["9.9.11", "9.9.10"], updates.Notes.Select(n => n.Version));
        Assert.True(updates.IsRequired);
        Assert.Contains("• إصلاح مهم", UpdateTexts.Changes(updates, new ArabicOnly()));
    }

    private sealed class ArabicOnly : Filee.Core.Localization.ILocalizer
    {
        public string Language => "ar";
        public string this[string key] => key;
        public string Format(string key, params object[] args) => key;
        public event EventHandler? LanguageChanged { add { } remove { } }
    }

    /// <summary>GitHub with 9.9.11 as the latest release, 9.9.10 required, a draft and an older release.</summary>
    private sealed class Releases : HttpMessageHandler
    {
        private static object Release(string tag, string body, bool draft = false) =>
            new { tag_name = tag, body, draft, prerelease = false, assets = Array.Empty<object>() };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            object? answer = path.EndsWith("/releases/latest", StringComparison.Ordinal)
                ? Release("v9.9.11", "<!-- update\nlevel: optional\nar: ميزة جديدة\n-->")
                : path.EndsWith("/releases", StringComparison.Ordinal)
                    ? new[]
                    {
                        Release("v9.9.12", "<!-- update\nlevel: optional\n-->", draft: true),
                        Release("v9.9.11", "<!-- update\nlevel: optional\nar: ميزة جديدة\n-->"),
                        Release("v9.9.10", "<!-- update\nlevel: required\nar: إصلاح مهم\n-->"),
                        Release("v1.0.0", "old"),
                    }
                    : null;
            return Task.FromResult(answer is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(answer), Encoding.UTF8, "application/json") });
        }
    }
}

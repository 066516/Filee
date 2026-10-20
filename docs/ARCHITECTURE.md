# Architecture

```
            ┌──────────────────────────── Filee.App (Avalonia) ─────────────────────────────┐
 global     │ TriggerService ──► GestureDetector ──► RadialController ──► RadialWindow/DonutMenu │
 mouse/keys │   (SharpHook)        (pure state         (drag / click       (transparent,          │
 ─────────► │                       machine)            mode)               topmost window)       │
            │                                              │ drop / click                         │
 Explorer   │ SingleInstance (pipe) ── --convert files ──►│                                      │
 context    │ Home drop zone ─────────────────────────────┘                                      │
 menu       │                                              ▼                                      │
            │                                    ConversionService ──► ToastWindow (progress)     │
            │ MainWindow (settings pages) ◄──► UserDataStore (JSON in %APPDATA%\Filee)            │
            └──────────────────────────────────────────────┬──────────────────────────────────────┘
                                                           ▼
            ┌──────────────────────────── Filee.Core ───────────────────────────────────────────┐
            │ JobQueue ──► RoutePlanner (Dijkstra over converter edges) ──► IConverter steps    │
            │ FormatRegistry · Preset · OutputPathResolver · ToolbarProfile · ProfileSelector   │
            └──────────────────────────────────────────────┬──────────────────────────────────────┘
                                                           ▼
            ┌──────────────────────────── Filee.Engines ────────────────────────────────────────┐
            │ Built in: Magick (images, RAW) · PdfSharp · Pdfium · PdfPig · Svg.Skia · ICNS      │
            │ HwpxWriter + DocxWriter over DocumentReaders (DOCX, XLSX, XLS, ODS, PPTX,          │
            │   PDF, HTML, EPUB, MOBI, FB2, HWPX, Markdown) · Spreadsheets · OOXML variants      │
            │   · E-mail · Data · E-books · CAD · Fonts · Subtitles · 3D models                  │
            │ Bundled: rhwp (HWP→PDF, HWP↔HWPX) · 7-Zip (archives) · Unhwp · Markdig             │
            │ Optional: FFmpeg · LibreOffice + H2Orestart · calibre · Ghostscript · Pandoc       │
            └────────────────────────────────────────────────────────────────────────────────────┘
```

## Flow of a conversion

1. **Trigger** – `TriggerService` receives global input from SharpHook and feeds `GestureDetector`.
   A drag gesture (modifier + drag past the threshold) opens the donut *before* files are known.
2. **Donut** – `RadialWindow` is transparent, topmost and not activated, so Explorer's drag continues.
   The first `DragEnter` delivers the file list; `RadialViewModel` picks a `ToolbarProfile` by extension
   (`ProfileSelector`: the first normal profile that has every file's extension; for a mix of types the first
   mixed-selection profile whose checked extensions cover the files, else the one with nothing checked) and asks
   `PresetAvailability` which presets can run (disabled slices get a reason).
3. **Drop** – the slice under the cursor is a preset. `ConversionService.Start` enqueues a `ConversionJob`.
   The drop is always reported as *Copy* so Explorer never deletes the source files.
4. **Plan** – for every file, `RoutePlanner` finds the cheapest chain of converter edges from the source format to
   the target (at most 3 hops). Engine priority (Settings → Engines) breaks ties. Some edges are limited: tracing
   only starts from raster images (`ImageSourcesOnly`), and PDF → PPTX picture slides are only ever a route of their
   own (`DirectOnly`), never a step of a longer one.
5. **Run** – `JobQueue` runs steps with per-engine concurrency limits. Intermediate files live in a temp folder;
   final outputs are named by `OutputPathResolver` (pattern, folder, conflict policy; the source is never overwritten).
   "Merge into one PDF" converts each file to PDF and merges with PDFsharp.
   "Compress into one archive" packs the dropped files, unconverted, with the archive engine (`IFileCombiner`).
6. **Report** – progress and results appear in the toast window and in Home → Recent conversions.

## Files dragged from a web browser

A picture, a link to a file or a download dragged out of a browser opens the donut like a file from the file manager.
`DraggedFiles.Read` asks `BrowserDrags` (App, reads Avalonia's drag data) first; `BrowserDrag` (Core, pure rules)
decides what in the drag is a file:

| Source | What the drag carries | What Filee takes |
| --- | --- | --- |
| Chrome / Edge, Windows | the picture as a virtual file (`FileGroupDescriptorW` + `FileContents`, which Avalonia 12 gives as a storage item without a local path), a `.url` virtual file for links, `UniformResourceLocatorW`, `text/x-moz-url`, `HTML Format` | the picture's own bytes; for a link, its address |
| Firefox, Windows | the picture written to `%TEMP%` (`CF_HDROP`), `text/x-moz-url`, file promise formats | the temporary copy (a local file in the temp folder *and* a web address on the drag) |
| Chrome / Firefox, Linux | `text/x-moz-url`, `_NETSCAPE_URL`, `text/html` (Avalonia turns `text/uri-list` into local files only and drops http addresses) | the address |
| Safari / Chrome, macOS | file promises (not receivable through Avalonia), `public.url`, `public.html`, sometimes picture data | the address, else the picture data |

Rules, in order: files handed over with their bytes (never `.url` shortcuts or programs); a picture (an HTML fragment
that is one `<img>` and no text, or a Firefox file promise); a `DownloadURL` a page sets; links only when the address
ends in an extension Filee converts and isn't a web page (`.html`, `.php`, none); last, picture pixels for a `blob:`
picture. Plain text is never read. `data:` addresses are decoded.

Until the conversion runs, the files stand as **placeholders** (`WebDrop`): paths in `%TEMP%\Filee\web\<drop>\drop\<n>\`
named like the file will be, so the donut picks the profile by extension without touching the network. No HEAD request
is made: a picture without an extension stands as `.jpg` and a link without one isn't offered. `ConversionService.Start`
gives the job a `WebDropFetcher` (`ISourceFetcher`): `JobQueue` saves every source first (half of its progress), then
converts. Failures are per file (`error.web_*`), cancelling stops a download, and the job toast/ring shows both phases.
While files are saved, a badge pops up above the donut's progress ring and above the toast's progress bar with the
speed and the amounts ("2.4 MB/s · 12.3 / 48.0 MB", "· 2 of 3 files" for several): `FileResult.Transfer` holds each
file's byte count, `ConversionJob.Transfer()` sums them, and `DownloadBadge` (App) averages the speed over the last
second with the Engines page's `TransferRate` and units, rewritten four times a second and shown after 300 ms.

- **Where**: Settings → General → "Files dragged from a browser" (`AppSettings.BrowserDrops`): the user's Downloads
  folder (`IPlatformServices.DownloadsFolder`: the shell's known folder on Windows, `XDG_DOWNLOAD_DIR` on Linux,
  `~/Downloads` on macOS) or a chosen one. "Next to the source" means this folder for these files
  (`OutputRule.SourceFolder`, set per job, never saved), so subfolder rules apply there and fixed folders stay fixed.
- **Save only the converted file**: the original goes to the drop's work folder, which `Release` deletes when the job
  ends; leftovers from a crash are deleted at start-up after a day.
- **Downloads** (`WebDownloader`): http and https only, at most 5 redirects (never from https to http or off the web),
  2 GB cap whether or not the server says the size, 30 s to answer and 30 s without data at most. No cookies or
  credentials are sent: a file behind a sign-in comes back as the sign-in page, which is refused as a web page
  ("save it from the browser first"). A browser's own bytes (virtual files, Firefox's copy) don't have that limit.
- **Names and safety**: Content-Disposition, else the page's name, else the address; then the first bytes decide the
  extension of pictures and PDFs (`.jpg` that is WebP becomes `.webp`). Names are cut to a plain file name (no folders,
  no device names, at most 120 characters) and never replace an existing file (`name (2).ext`). Programs, installers and
  scripts are refused by extension, media type and first bytes; nothing downloaded is ever run, and every file goes
  through the same converters as any other.

## Donut control

`Controls/DonutMenu.cs` draws the ring itself (no child controls) for speed. `DonutGeometry.cs` holds the pure math
(slice angles, hit testing) and is unit tested. The same control is the live toolbar and, with `IsEditMode`, the
editor on the *Donut toolbar* settings page (drag to reorder, ✎ / right-click to edit).
Animations are small springs advanced per frame; `Motion.Enabled == false` (*Reduce animations*) snaps them.

## Settings and data

`UserDataStore` owns `settings.json`, `presets.json`, `profiles.json` and `history.json`
(System.Text.Json source generation, atomic writes, broken files are kept as `*.broken`).
Bump `AppSettings.CurrentSchemaVersion` and add a step to `SettingsMigrations` when the format changes.

## Localization and theming

- `LocalizationService` loads `Assets/i18n/<lang>.json` into `Application.Resources`; XAML uses
  `{DynamicResource key}`, so switching language updates the UI live.
- `ThemeService` writes the accent colour, corner radii, fonts and Fluent's `SystemAccentColor*` into
  application resources. Light/dark surface colours are theme dictionaries in `Styles/Soft.axaml`; a user's
  `theme.json` (`CustomTheme`, see [CUSTOM-THEMES.md](CUSTOM-THEMES.md)) overrides them with theme dictionaries in
  application resources, which are searched first.

## Command line and watch folders

- `Filee.Cli` (`filee-cli.exe`, run as `filee` through `cli\filee.cmd`, which Setup can put on PATH) composes the same
  pipeline as the app without any UI (`CliHost`: `UserDataStore`, `EngineRegistry`, `ConverterCatalog`, `JobQueue`)
  and reads the app's presets and engine order. It is published into the app folder, so both share one runtime and
  the bundled engines; its name can't be `filee.exe` next to `Filee.exe` on a case-insensitive file system.
- `Filee.Core/Watching/FolderWatcher` watches one `WatchRule` with a `FileSystemWatcher` and converts a file once it
  has kept its size and time for a moment and opens without sharing (downloads and copies in progress are skipped).
  Temporary files, unknown formats and the output and originals folders are ignored, so outputs never loop back.
  The tray app runs one per rule (`WatchFolderService`, Settings → Watch folders, conversions in the toast and
  history); `filee watch` runs one on its own.

## Platform code

Everything OS-specific goes through `IPlatformServices` (`Filee.Core/Platform`). Windows lives in
`Filee.Platform.Windows`; macOS will get its own project. Global input uses SharpHook on both.

On macOS, files reach Filee from file managers other than Finder too (issue #160). The "Convert with Filee" service
(`MacOSServicesProvider`) and the donut's drags (`DraggedFiles`) read file URLs first, then the older
`NSFilenamesPboardType` array of paths (`PasteboardPaths`); the service also takes plain-text lines that are absolute
paths. The donut registers those types with AppKit next to Avalonia's own, so such a drag reaches it, and a drag or
service request without readable files is logged with the types it offered. File promises (`NSFilePromiseProvider`)
can't be received through Avalonia; for a browser drag Filee reads the address and HTML it also offers, which the donut
registers too (see "Files dragged from a web browser"). A click-mode donut opened for a later launch or the service forgives one loss of
focus within a second of opening (`ActivationGrace`): Filee becoming the active app can take focus away from it.

## Explorer integration

Settings → General → "Show “Convert with Filee” in the Explorer context menu" (`AppSettings.ContextMenuEnabled`) is
applied by `WindowsPlatformServices.SetContextMenu` at start-up and whenever settings change (installed builds only):

- **Classic verb** `HKCU\Software\Classes\*\shell\Filee` (`Filee.exe --convert "%1"`, one process per file; the running
  instance collects them through `SingleInstance`) and a **Send To** shortcut. Per user, no admin rights. On Windows 10
  this is the entry; on Windows 11 it sits under "Show more options".
- **Windows 11 top-level entry** (optional): an `IExplorerCommand` in `FileeExplorerMenu.dll`
  (`src/Filee.ExplorerMenu`, plain C, no C runtime) declared by the sparse package `FileeExplorerMenu.msix`
  (`ExplorerMenuPackage.CreateManifest`: `desktop4:FileExplorerContextMenus`, `desktop5:ItemType Type="*"`,
  `com:SurrogateServer`, `uap10:AllowExternalContent`, `AppListEntry="none"`). Both files ship next to Filee.exe and the
  package's external location is the install folder (`C:\Program Files\Filee` by default).
  - The package is **unsigned** (publisher contains `OID.2.25.311729368913984317654407730594956997722=1`; Filee has no
    code-signing certificate). Windows only registers an unsigned package with executable content **with
    administrator rights** (it installs it for all users); a per-user attempt fails with 0x80073D2B "an unsigned
    package cannot include Executable activations". Setup (`installer/Filee.iss`) has those rights anyway and
    registers it on Windows 11 x64 while its option is ticked. Later the General page offers "Add to the main menu",
    which runs `Add-AppxPackage -Path … -ExternalLocation … -AllowUnsigned` in Windows PowerShell through one UAC
    prompt (`ExplorerMenuRegistration`). A signed package would register per user without a prompt.
  - The manifest version (`ExplorerMenuPackage.ManifestVersion`) is independent of the app version, so app updates
    don't need a new prompt: the DLL is loaded from the install folder and updated with the app. Bump it only when the
    manifest changes; the page then offers to add the entry again ("Outdated").
  - The DLL shows the entry only for files (not folders or items inside ZIP folders) and only while
    `%APPDATA%\Filee\explorer-menu.txt` exists. The app writes the title in the UI language there while the setting is
    on and deletes it when the setting is off, so switching the setting takes effect without re-registering. While the
    package is registered the classic verb is removed (Windows lists packaged commands under "Show more options" too).
  - Invoke starts `Filee.exe --convert "<path>"…` next to the DLL and returns at once. Selections longer than a
    command line go through `%TEMP%\Filee-convert-*.txt` (`--convert-list`, one path per line), which the app deletes.
  - Updates: Setup asks the running Filee to exit (`Filee.exe --quit`), then renames a `FileeExplorerMenu.dll` that
    Explorer still has loaded (Windows allows renaming a loaded DLL, not replacing or deleting it) and has Windows
    delete the old copy at the next restart, so updating needs no restart. An update keeps the registration as it
    was and refreshes it. Uninstall runs `Filee.exe --uninstall-cleanup` (`UninstallCleanup`): it removes the package,
    the title file, the classic verb, the startup entry and downloaded engines; settings stay.
  - Build: `pwsh build/build-explorer-menu.ps1` downloads a pinned Zig (SHA-256 checked) into `build/.cache`, compiles
    the DLL and packs the MSIX with `build/tools/make-explorer-package.cs` (Windows' own packaging API, no SDK).
    Output: `build/.cache/explorer-menu`; CI builds it for the tests, `build/build-installer.ps1` builds it into the
    publish folder.

Debugging: `Get-AppxPackage Filee.ExplorerMenu` shows the registration (`InstallLocation` is the external location);
`Get-AppPackageLog -ActivityID <id>` explains a failed deployment. To try a local build, copy
`FileeExplorerMenu.dll` + `.msix` next to an installed Filee.exe and use the button, or run elevated:
`Add-AppxPackage -Path FileeExplorerMenu.msix -ExternalLocation <folder with Filee.exe> -AllowUnsigned`. Explorer picks
the entry up for new menus; if not, restart Explorer. To remove it by hand:
`Get-AppxPackage Filee.ExplorerMenu | Remove-AppxPackage`.

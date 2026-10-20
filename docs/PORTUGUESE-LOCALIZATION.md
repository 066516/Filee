# Portuguese localization review

Initial baseline: `c63a261b516316101d10709fbc61f43593ba184f`. Updated to
`85f7e56156b373e889a725e7d245d8024b3d5f70` when upstream merged #282 during this work (20 October 2026).
This review covers localization and the UI problems reproduced while adding Portuguese. It is not a completed
security, conversion-fidelity or performance audit.

## What is included

Both `pt-PT.json` and `pt-BR.json` translate all 792 original English entries. Eighteen descriptive engine names
were still supplied directly by C# metadata; those now have resource keys in all ten languages. Each Portuguese
catalog also includes all 59 installer messages from #282: 869 entries, in English key order.
Existing keys, product names, format identifiers,
numeric placeholders, line breaks, filename tokens and CLI switches are preserved. The literal `Photo_` in the
rename recipe is kept because that is what the recipe actually writes.

The new Windows installer embeds the same catalogs and shares `AppLanguages.All` with the app. Both variants
appear automatically in its language picker. A shared Portuguese region policy also handles neutral Portuguese,
script subtags and the legacy `ptpt` / `ptbr` installer codes. An explicitly chosen setup language is passed to
Filee through upstream's new `--language=` option.

`LocalizationService` publishes JSON strings as dynamic Avalonia resources. English remains the runtime fallback;
unrecognized keys return the key. Code-generated pages rebuild on `LanguageChanged`, and toggle labels use
the same localized resources. The new languages require no settings migration and use the existing Latin fonts.
Early startup messages use the same OS-language resolver before Avalonia starts.

| Setting / OS language | Result |
| --- | --- |
| Explicit `pt-PT`, on any OS language | pt-PT |
| Explicit `pt-BR`, on any OS language | pt-BR |
| Automatic + `pt-BR`, including `pt-Latn-BR` | pt-BR |
| Automatic + `pt`, `pt-PT`, `pt-AO`, `pt-MZ`, `pt-CV` | pt-PT |
| Automatic + unsupported language | English |

Codes are case-insensitive. Automatic selection uses macOS's preferred language when available, otherwise
`.NET`'s installed UI culture. Changing the UI language does not override the user's regional number or date
format. Playback multipliers and the frame-count limit now respect that regional culture too. Machine-readable
conversion settings remain numeric and keep their existing serialization.

Count summaries use natural count labels such as `Conversões: 1`, avoiding English-style `(s)` and incorrect
singular/plural agreement. The tests cover zero, one, two and twenty-one. This does not introduce a general
CLDR plural engine; a future message needing grammatical plural categories should add a dedicated contract.

## Terminology

| English | pt-PT | pt-BR |
| --- | --- | --- |
| File | Ficheiro | Arquivo |
| Save | Guardar | Salvar |
| Settings | Definições | Configurações |
| Preset | Predefinição | Predefinição |
| Profile | Perfil | Perfil |
| Donut toolbar | Barra circular | Barra circular |
| Watch folders | Pastas monitorizadas | Pastas monitoradas |
| Engines | Motores | Mecanismos |
| Download | Descarregar | Baixar |
| Frame | Fotograma | Quadro |
| Mouse | Rato | Mouse |
| Default | Predefinido / de origem | Padrão |
| About | Acerca de | Sobre |

European Portuguese uses AO90 and informal `tu`. Brazilian Portuguese uses `você` and its corresponding
imperatives. Presets are reusable conversion settings; profiles choose the toolbar for a file selection, so those
two concepts keep different terms. Technical third-party notices remain in their original English.

## Reproduced findings

P0 = critical, P1 = high, P2 = medium, P3 = low. No P0/P1 conclusion about the wider application is implied.

| Priority | Evidence and reproduction | Resolution |
| --- | --- | --- |
| P2 | Select Portuguese and open the 440 px close dialog. The tray action exceeds the window. Both new bounds tests failed before the fix. | Wrap actions onto another row; keep every action and its text inside the window. |
| P2 | Open Toolbar, Theme or Presets at the existing 820 px minimum window width. Fixed side columns crush or overlap the central editor. Optional-engine descriptions also get very narrow. | Give these editors usable minimum content widths and local horizontal scrolling. The main window size and vertical scrolling remain supported. |
| P2 | Change the regional culture to pt-PT or pt-BR. The animation-speed choices and frame-count limit still use invariant decimal/group separators. | Use `CurrentCulture` for those display values; test that applying a choice still saves the same numeric value. |
| P2 | Inspect `EngineItemViewModel.Name`: descriptive names come directly from `Converter.DisplayName`, bypassing the catalog. | Add 18 name keys in every language, with metadata fallback for unknown engines. Product names stay intact. |
| P2 | Inspect the two icon-only preset actions: tooltips are localized but explicit accessible names are absent. | Bind their automation names to the existing translation keys and test both variants. Native screen-reader behavior still needs platform validation. |
| P3 | Catalog tests maintain a second language list, so a newly registered language can escape coverage. Duplicate JSON keys are silently overwritten on dictionary deserialization. | Derive coverage from the registry; verify file/registry parity, unique keys and non-empty entries. |

Portuguese tests additionally compare the multiplicity of numeric placeholders, named pattern tokens, newline
counts and Unicode normalization, and parse/format every numeric composite format. The existing global
set-based placeholder test is retained: other languages may legitimately avoid repeating an argument.

## Validation

Run from the repository root:

```sh
dotnet build Filee.slnx -c Release
dotnet test tests/Filee.App.Tests -c Release --filter "FullyQualifiedName~Localization|FullyQualifiedName~Portuguese|FullyQualifiedName~InstallerTests"
dotnet test Filee.slnx -c Release
dotnet format Filee.slnx --verify-no-changes --no-restore --severity warn
```

Recorded on Linux x64 with .NET SDK 10.0.402 and the project's 12 pinned Noto fonts:

| Check | Passed | Failed | Skipped | Scope |
| --- | ---: | ---: | ---: | --- |
| Release solution build | — | 0 errors | — | Includes the new Setup project; 10 existing warnings |
| App localization/Portuguese/installer contracts | 151 | 0 | 0 | Final code after #282 |
| Setup texts, all UI states and language handoff | 49 | 0 | 1 | Final code; the Windows-only real-path assertion is skipped on Linux |
| Core | 427 | 0 | 0 | Full suite |
| CLI | 27 | 0 | 0 | Full suite |
| Platform | 46 | 0 | 5 | Full suite; native platform cases skipped |
| Engines | 1139 | 6 | 40 | Broad run on the initial baseline; conversion source is unchanged by this patch |

The final passing checks above total 700 tests, with 6 platform-specific skips. The broad engine run is reported
separately and is not included in that passing total.

Five engine failures reproduce on a separate, untouched checkout of `c63a261`: CAD's Korean SVG text, PDF→SVG
through LibreOffice, and three Pandoc markup cases (the installed Pandoc lacks the AsciiDoc reader). The remaining
LibreOffice flat-document case exited with 137 in the broad run but passed both on clean main and in a focused
recheck on this branch. The failed broad run is retained; these results do not constitute a clean full-suite pass.

The unfiltered App run hit three `SingleInstanceTests` failures with `SocketException: Permission denied` from
named-pipe sockets. It did not finish and was interrupted; an additional broad run excluding that class also did
not finish and was interrupted. Neither is counted as a successful App run. The focused UI/localization suite
completed normally. Solution-level `dotnet format` is blocked by the same denied named-pipe transport in Roslyn's
build host; folder-based whitespace verification of changed C# files and `git diff --check` pass. The PR remains
a draft for native Windows/macOS and complete CI validation. No remote CI run was available at the last check. The native Windows path check remains enabled on Windows;
only its unsupported Linux execution is skipped.

`RenderTests.Pages` includes all nine pages in both Portuguese variants at 1080 px. The dedicated Portuguese
render suite covers all nine pages at 820 px, light/dark themes and 100%/200% render scaling, plus close-dialog
bounds, tagline bounds and accessible names. Setup also captures both variants in its welcome, options,
installing, completed, update-failure and uninstall states, in light/dark themes. The app captures the far-right
and bottom portions of scrollable editors as well.
Screenshots are generated in the test output's `screenshots/` directory; they are not runtime app assets.

Native Windows/macOS desktops, real Windows installation/uninstallation and assistive technologies require their own
checks. Headless Skia screenshots and simulated OS-language inputs do not certify native desktop integration.
The CLI has its own deliberately English-only text provider and is unchanged by these app translations.

## Existing PR integration

All six open upstream PRs were read before implementation: #242, #249, #275, #282, #283 and #291.
No unmerged feature or issue fix was imported. The merged installer was integrated from upstream.

- #282 merged while validation was finishing. Its installer and shared language registry were integrated from
  upstream, the obsolete Inno changes were removed, and all 59 new keys were translated into both variants.
  The updated app and Setup localization/render suites were then rerun.
- #291 adds Arabic and changes `LocalizationService`; preserve Arabic alongside both Portuguese variants.
  Its two extra strings also need Portuguese translations when that work lands.
- #275/#291 touch Theme and #283 adds Live/Motion Photo creation strings. Preserve the narrow-window guard
  when resolving XAML and translate any new English keys rather than copying whole catalogs from either branch.
- #242 is translated documentation and #249 is automatic labeling. Neither feature is part of this change.

## Follow-up audit scope

Phase 2 remains separate: untrusted input and archive extraction, external processes/downloads, temporary files,
cancellation/concurrency, conversion fidelity/metadata/color, memory and performance, dependencies, packaging,
native compatibility, keyboard/screen-reader accessibility and CI coverage. Findings need reproducible evidence
before being reported as vulnerabilities or fixed in independent patches.

# Independent Portuguese review: follow-up

The independent review examined b694dad, based on Filee 2.0.0 at 799e394. The first follow-up integrated upstream
a121a3c9e3254918f8b171bb7f675b0bdc532244 on 22 October. The current integration is
984475954a139fa8beee0ca3e7b6b4c8375ef0f2, verified on 24 October 2026. Code is pushed only to Blackspirits/Filee;
the contributor has authorized submitting the pull request to KnifeLemon/Filee.

## Native chooser correction - 24 October 2026

An isolated native launch exposed the long pt-PT Calibre name pushing its system-copy badge outside the first-run
card. A deterministic regression fails before the fix (579 px within a 508 px card) and passes after names/badges
wrap. All fifteen affected render, geometry, short-screen and zoom cases pass, including both Portuguese variants.
The actual application also responds to Ctrl+plus (110%) and Ctrl+0 (100%). This is additional native evidence;
installation, OS-language reversal, permissions and screen readers remain unvalidated.

## Submission integration: FITS, CAD and Turkish - 24 October 2026

The submission includes upstream `98447595`, retaining its FITS/CAD/GNOME features and Turkish translation.
All fourteen new source keys and the revised Wayland message are translated in pt-PT. Existing Brazilian and
other community values are retained. Turkish receives only the same nineteen English engine-name fallbacks
and four compact labels used by the contribution; compact labels reuse its existing translations and the
upstream catalog layout/order is preserved. The shared language recording is regenerated for all sixteen
languages; the other fourteen media files are unchanged. App/Setup are rebuilt after generation.

Ten remaining render fixtures now dispose their directly constructed MainWindowViewModels, releasing service
subscriptions. This does not establish a measured CI speedup. The UI-scale and FITS fixtures cover both Portuguese
variants. Upstream itself increased the portable-job timeout from 60 to 90 minutes; this contribution does not
add another timeout change. Current results and the host-specific Calibre baseline failure are recorded in the
native-validation report. Unavailable native sessions remain explicit follow-ups; themes and Phase 2 have not started.

## Submission integration: UI scale and PDF tables — 23 October 2026

Upstream main advanced to `e3c6536a` while the full native matrix was running. This integration preserves the
new UI-scale controls and PDF-table converter, translates their eight new source keys in pt-PT, and adds the
nineteenth descriptive engine name (`Tabelas PDF` / `Tabelas de PDF`) under the existing fallback policy. All
fifteen catalogs contain 1,034 keys. No existing translation value is rewritten, including the upstream Brazilian
messages. Fifteen adjacent JSON/Theme-page conflicts retain both the upstream functionality and this
contribution's scrolling/layout changes. The existing UI-scale Theme-page test also runs in both Portuguese
variants and produces independent captures. Additional theme packs and Phase 2 remain separate future work.

## Final upstream catch-up — 23 October 2026

Merge `614cb4d5` incorporates the next 48 upstream commits, including spreadsheet printing, PDF finishing and
update rollback handling. All 47 new keys are translated in pt-PT; the fifteen catalogs now each contain 1,025
keys. Existing Portuguese key order, format items, line breaks and NFC are preserved. New Brazilian and other
community translations remain upstream values; the seven earlier targeted Brazilian corrections are unchanged.

A minimum-width print-studio capture reproduced two pt-BR text overflows: a workbook choice and the automatic
page-height caption. Choices and page-width/height captions now wrap; geometry cases retain English and both
Portuguese variants. The existing PDF-finishing fixture also runs for pt-PT/pt-BR. A fixture cleanup error during
development was corrected by retaining the window's ownership of its view model; no production lifecycle or
assertion was weakened. The native-validation report separates these current results from historical snapshots.

## Findings and responses

| Finding | Evidence checked and response |
| --- | --- |
| P1-1: upstream conflicts and outdated coordination | Reproduced the original ten conflicts, merged French/Traditional Chinese and later current upstream/Nepali, retaining both Portuguese variants and both sets of tests. Preserve published history with merges. Add missing new resource keys and update coordination notes. |
| P1-2: CI duration | Run 39695610117 had 8/9 successes; Intel macOS hit the 60-minute job limit while individual tests were still passing. This does not prove a single hung test. Three render fixtures failed to dispose directly constructed MainWindowViewModels, leaving service subscriptions alive; they now use using. Reduce 72 minimum-width cases to 54: both themes at 1x and every language/page at 2x light. PNG encoding is opt-in; assertions always run. No timeout is increased and no failed cases are skipped. Native CI 39800321618 later passed all nine jobs; Intel macOS's App suite completed in 27.1291 minutes. See the native validation report. |
| P1-3: broad Brazilian rewrite | Preserve #322's community wording; reduce the diff to seven existing entries for count agreement and the pointer reference. Restore no Dock, distinct Redefinir/Restaurar, Gravar, Relatar, E-books, curly quotes and the one-time contribution. Identical wording across variants is not itself evidence of an error. |
| P2-1: Apple names | Apple's explicit navigation instructions use Definições do Sistema, Privacidade e Segurança and Automatização. Correct the Portuguese permission path. Native display remains to be checked. |
| P2-2: license glossary | Software livre accurately describes the original English; this is glossary consistency, not a mistranslation. Use software de código aberto consistently in pt-PT. |
| P2-3: demonstration context | Remove four abbreviated Portuguese browser/flip replacements; retain complete shared desktop recordings and translated captions. Regenerate the other recordings after wording changes. History sample paths match the recording OS. Portuguese desktop recordings remain a follow-up. |
| P2-4: default-width regression | Three new cases reproduced 800 px of toolbar content in a 786 px viewport at 1080 px, including English. Set the content minimum to 786 px. Further visual inspection found a clipped New preset button and long preview labels: actions wrap, and editor preview uses runtime compact names. Full palette/editor names remain. Default-width cases cover every registered toolbar locale, including RTL. |
| P2-5: unreviewed third-language engine names | Use English values for the 18 new engine keys in other languages pending maintainer review, preserving previous metadata display. Compact labels reuse already translated full names. No native review is claimed for those languages. |
| P3: quotes, percentages and pronouns | Correct pt-PT quotation style and pointer reference, restore Brazilian community quotes, use 50% in compact labels. Literal folder names and commands are protected without requiring a particular quotation style or untranslated argument descriptions. |
| P3: clipped action/profile names | Extrair vídeo is a compact label for Extract Live Photo video; full editor names remain. Use Ficheiros mistos and the community's Arquivos mistos. Retain upstream's new caption wrapping. |
| P3: regional formats and existing choices | Keep CurrentCulture for display and numeric settings serialization; tests cover mismatched UI/number regions. Document neutral pt → pt-PT as project policy. Explicit saved pt-BR choices are preserved. Finder regional initialization needs native verification. |
| P3: pre-existing accessibility and CLI gaps | Native NVDA/VoiceOver/keyboard checks remain. Other unnamed icon controls and CLI localization preserving JSON/identifiers require separate Phase 2 work. |

## Selected pt-PT corrections

| Key | English source | Final pt-PT |
| --- | --- | --- |
| about.license | Filee is free software under the MIT license. | O Filee é software de código aberto, ao abrigo da licença MIT. |
| error.too_many_frames | Too many frames to save as pictures, raise "Every Nth frame" | Há demasiados fotogramas para guardar como imagens. Aumenta o valor de «A cada N fotogramas» |
| preset.half_size.donut | Half size | Tamanho 50% |
| preset.extract_motion.donut | Extract video | Extrair vídeo |
| profile.mixed | Mixed files | Ficheiros mistos |

## Local validation before native CI

The later [native validation](PORTUGUESE-NATIVE-VALIDATION.md) closes the CI/full-formatter checks on code commit
9d357e7. The local results below remain the record of the Linux baseline comparison.

Validation uses .NET SDK 10.0.402 on Linux x64 and all fifteen SHA-256-verified pinned Noto files.
The Release solution build succeeds with zero errors and eleven existing obsolete-API/nullability warnings.

| Check | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| App localization, Portuguese rendering, toolbar, release/installer contracts | 327 | 0 | 0 |
| Setup full suite on Linux | 131 | 36 | 1 |
| Core full suite | 460 | 0 | 0 |
| CLI full suite | 28 | 0 | 0 |
| Platform full suite | 48 | 0 | 5 |
| Unique total across these runs | 994 | 36 | 6 |

The initial 41-case smoke selection is included in the 327 App passes and is not counted twice. It covers the
23 default-width cases (four editors in English/Portuguese and toolbar actions in eleven other languages), both
catalog integrity cases, the contextual glossary and live language switching. The broader App selection also covers
54 minimum-width cases, donut text layout, native identifiers, toolbar behavior and localized slideshow contracts.
This is a focused App run, not a full-solution green result. Earlier interrupted broad runs are not certification.

All 36 Setup failure names exactly match a clean a121a3c checkout in the same Linux environment: its non-UI suite
has 53 passed / 36 failed. Windows-style fixture paths, PE imports and registry/path assumptions remain the cause.
No production path behavior or test exclusions were changed to conceal these failures. Both media generators
completed for the refreshed more clips; explicit generation runs are excluded from the test total. All fifteen
shared/Portuguese WebP files have valid poster indices and fit the existing per-file/pack budgets (~3.87 MiB total).

Full solution formatting cannot connect to this environment's build host pipe (SocketException 13; host exit 137).
Changed-file whitespace verification and git diff --check pass. The later Windows CI job passed the full formatter.

Representative commands, after restoring packages and running build/fetch-fonts.ps1:

```sh
dotnet build Filee.slnx -m:1 -c Release
FILEE_LOCALIZATION_SCREENSHOTS=1 dotnet test tests/Filee.App.Tests -c Release --no-build --filter "FullyQualifiedName~Localization|FullyQualifiedName~Portuguese|FullyQualifiedName~DonutLabelPlacement|FullyQualifiedName~ToolbarEditorTests|FullyQualifiedName~WhatsNewTests|FullyQualifiedName~InstallerTests|FullyQualifiedName~SupportersTests|FullyQualifiedName~ReleaseNotesTests|FullyQualifiedName~Live_photo_options_render_for_jpg_and_heic_and_save_only_there"
dotnet test tests/Filee.Setup.Tests -c Release --no-build
dotnet test tests/Filee.Core.Tests -c Release --no-build
dotnet test tests/Filee.Cli.Tests -c Release --no-build
dotnet test tests/Filee.Platform.Tests -c Release --no-build
# On the clean upstream worktree, with the same SDK/environment:
dotnet test tests/Filee.Setup.Tests -m:1 -c Release --filter "FullyQualifiedName!~SetupTextsTests&FullyQualifiedName!~SetupWindowTests"
```

Inspected fresh Skia captures include pt-PT and French default toolbars, Portuguese theme/history recordings, and
the corrected complete action buttons and compact preview labels. The automated suite additionally captures the
Portuguese pages/dialogs and installer states. Real installation/UAC, Finder and permission-prompt display,
and keyboard/NVDA/VoiceOver checks remained open at that snapshot. Historical native CI passed all nine jobs; the
[native validation report](PORTUGUESE-NATIVE-VALIDATION.md) records the evidence and interactive checklist.
That historical head stayed a draft; current readiness and native limitations are recorded below.

## Approved Phase 1 follow-up — 23 October 2026

The remote contribution branch was confirmed at `484d6fb`, then advanced by the small editorial commit
`91174f2` and generated-media commit `ff7b349`. Only pt-PT's sponsor action/title changed to `Apoiar` /
`Apoiar o Filee`. `Página inicial`, `patrocinadores`, all 911 keys, variables and formatting remain intact;
pt-BR and the other JSON catalogs are unchanged. The glossary has five additional existing-fixture cases and
the sponsor-window render fixture now includes both Portuguese variants.

[The updated native report](PORTUGUESE-NATIVE-VALIDATION.md#editorial-follow-up--23-october-2026) records the
339 focused App passes, 168 Setup passes after repairing the environment's screenshot output, full Core/CLI/Platform
results and the separate 155-case check after the final resource rebuild. It distinguishes transient output adapters
from committed code, and local full-format diagnostics from the successful formatter step in the new native CI.

The real isolated Windows app passed the pt-PT automatic → explicit pt-BR → full exit/restart → automatic sequence.
Selected native focus/preset keyboard checks and the new support title were observed. Reverse OS-language,
mismatched native numeric region, modal keyboard, real release installation/UAC/Explorer, macOS/Finder/permissions
and NVDA/VoiceOver checks remained open. That earlier head stayed a draft; current readiness is recorded below.

Commands used for this follow-up (PowerShell, Release):

```powershell
dotnet restore Filee.slnx --disable-parallel
./build/fetch-fonts.ps1
dotnet build Filee.slnx -m:1 -c Release --no-restore
dotnet format Filee.slnx --verify-no-changes --no-restore --severity warn
dotnet format Filee.slnx --verify-no-changes --no-restore --severity warn --include tests/Filee.App.Tests/PortugueseLocalizationTests.cs tests/Filee.App.Tests/RenderTests.cs

$env:FILEE_WHATSNEW_LANGUAGE = 'pt-PT'
$env:FILEE_WHATSNEW_ONLY = 'theme,more'
./tests/Filee.App.Tests/bin/Release/net10.0/Filee.App.Tests.exe -method Filee.App.Tests.WhatsNewMedia.Generate_2_0_0 -explicit only
$env:FILEE_WHATSNEW_LANGUAGE = 'en'
$env:FILEE_WHATSNEW_ONLY = 'languages'
./tests/Filee.App.Tests/bin/Release/net10.0/Filee.App.Tests.exe -method Filee.App.Tests.WhatsNewMedia.Generate_2_0_0 -explicit only

$env:FILEE_LOCALIZATION_SCREENSHOTS = '1'
$appFilter = 'FullyQualifiedName~Localization|FullyQualifiedName~Portuguese|FullyQualifiedName~DonutLabelPlacement|FullyQualifiedName~ToolbarEditorTests|FullyQualifiedName~WhatsNewTests|FullyQualifiedName~InstallerTests|FullyQualifiedName~SupportersTests|FullyQualifiedName~ReleaseNotesTests|FullyQualifiedName~Live_photo_options_render_for_jpg_and_heic_and_save_only_there|FullyQualifiedName~Sponsor_window_renders'
dotnet test tests/Filee.App.Tests -c Release --no-build --no-restore --filter $appFilter --logger trx
dotnet test tests/Filee.Setup.Tests -c Release --no-build --no-restore --logger trx
dotnet test tests/Filee.Core.Tests -c Release --no-build --no-restore --logger trx
dotnet test tests/Filee.Cli.Tests -c Release --no-build --no-restore --logger trx
dotnet test tests/Filee.Platform.Tests -c Release --no-build --no-restore --logger trx

# After restoring the original helpers and rebuilding all embedded assets:
dotnet build Filee.slnx -m:1 -c Release --no-restore
$resourceFilter = 'FullyQualifiedName~Localization|FullyQualifiedName~The_2_0_0_deck_ships_with_its_media_and_texts|FullyQualifiedName~Slide_media_stay_small|FullyQualifiedName~Portuguese_recordings_preserve_the_desktop_context_and_localize_the_other_slides|FullyQualifiedName~Changing_language_refreshes_the_current_recording_without_resetting_the_slide'
dotnet test tests/Filee.App.Tests -c Release --no-build --no-restore --filter $resourceFilter --logger trx
```

The successful media/capture commands used temporary output-folder adapters as documented in the native report;
those adapters are not repository features or committed changes. The local logs retain failed attempts as well as
final successes. Local commits were created without SSH signatures because the configured signing key required an
unavailable passphrase; the user's signing configuration was not changed.

## Current upstream integration and final review — 23 October 2026

The remote was checked before editing and again before publication. Merge `47616f7` brings upstream `4c3a3e1`
into the contribution without resetting or rewriting published history. Seventeen merge conflicts were resolved;
there were no competing value changes in the three-way JSON merge. Material You, OCR, palette tabs, Thai and all
other upstream changes are retained. All fifteen catalogs have 978 keys; the 67 missing pt-PT translations and
three changed-source messages are complete. Upstream Brazilian wording is retained key by key. Catalog section
spacing is preserved. Thai receives English engine-name fallbacks and its own existing full compact-label values,
following the existing contribution policy; no new native-language review is claimed for it.

Approved final pt-PT edits: `sponsor.thanks` is `Obrigado a quem apoia o Filee`; `merge.order_hint` explains that
files are combined from the top of the list to the bottom. The final approved CLI hint uses `<pasta>` and `<formato>`,
matching pt-BR and several other translated catalogs. They describe the path and format that the user supplies;
`filee watch` / `--to` remain literal syntax recognized by the parser.
`Página inicial`, `Apoiar` and `Apoiar o Filee` remain. The contextual glossary follows the approved acknowledgement.

Commit `fea4fa0` fixes the old CI's Setup slide-timer race without changing production behavior or dropping an
assertion. The media commit regenerates all eight Portuguese clips and the shared multilingual showcase; complete
browser/flip desktop recordings remain. Both App and Setup embed the regenerated files after the final build. The existing supporter render fixture
also covers en, pt-PT and pt-BR and asserts that the acknowledgement fits in both windows.

See the [current native report](PORTUGUESE-NATIVE-VALIDATION.md#current-upstream-integration--23-october-2026)
for final counts, the temporary build-copy workaround and separate headless/native evidence. The contributor has
accepted unavailable native sessions as explicit follow-ups for maintainer review. The full port-package release
matrix in CONTRIBUTING.md is not an indefinite gate for submitting this translation PR. Actual current CI and
concrete integration failures still gate readiness. The fork PR remains the coordination point; no upstream code
write, PR merge or Phase 2 work is included.

Commands for the integrated snapshot (run from the temporary copy when checkout output is refused):

```powershell
./build/fetch-fonts.ps1
dotnet restore Filee.slnx --disable-parallel
dotnet build Filee.slnx -m:1 -c Release --no-restore -p:UseSharedCompilation=false
# FILEE_WHATSNEW_LANGUAGE=pt-PT, then pt-BR; FILEE_WHATSNEW_ONLY=formats,crop,theme,more
# Then en with FILEE_WHATSNEW_ONLY=languages. Rebuild App/Setup afterwards.
./tests/Filee.App.Tests/bin/Release/net10.0/Filee.App.Tests.exe -method Filee.App.Tests.WhatsNewMedia.Generate_2_0_0 -explicit only
$env:FILEE_LOCALIZATION_SCREENSHOTS='1'
$appFilter='FullyQualifiedName~Localization|FullyQualifiedName~Portuguese|FullyQualifiedName~DonutLabelPlacement|FullyQualifiedName~ToolbarEditorTests|FullyQualifiedName~ToolbarPaletteViewTests|FullyQualifiedName~ThemePageTests|FullyQualifiedName~MaterialYouThemeTests|FullyQualifiedName~WhatsNewTests|FullyQualifiedName~InstallerTests|FullyQualifiedName~SupportersTests|FullyQualifiedName~ReleaseNotesTests|FullyQualifiedName~Live_photo_options_render_for_jpg_and_heic_and_save_only_there|FullyQualifiedName~Sponsor_window_renders'
dotnet test tests/Filee.App.Tests -c Release --no-build --filter $appFilter --logger trx
dotnet test tests/Filee.Setup.Tests -c Release --no-build --logger trx
dotnet test tests/Filee.Core.Tests -c Release --no-build --logger trx
dotnet test tests/Filee.Cli.Tests -c Release --no-build --logger trx
dotnet test tests/Filee.Platform.Tests -c Release --no-build --logger trx
dotnet format Filee.slnx --verify-no-changes --no-restore --severity warn
dotnet format Filee.slnx --verify-no-changes --no-restore --severity warn --include tests/Filee.App.Tests/PortugueseLocalizationTests.cs tests/Filee.Setup.Tests/SetupWindowTests.cs tests/Filee.App.Tests/RenderTests.cs
git diff --check
```

## References

- [Apple pt-PT automation permissions](https://support.apple.com/pt-pt/guide/mac-help/mchl108e1718/mac)
- [Apple pt-PT Dock](https://support.apple.com/pt-pt/guide/mac-help/mh35859/mac)
- [Apple pt-BR Dock](https://support.apple.com/pt-br/guide/mac-help/mh35859/mac)
- [Reviewed CI run](https://github.com/Blackspirits/Filee/actions/runs/39695610117)

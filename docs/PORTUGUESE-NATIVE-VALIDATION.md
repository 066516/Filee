# Portuguese native validation

Historical CI-validated code commit: `9d357e7bcb6fd389d2fa8e48faa1ba3f8c39b13d`.
Baseline: `a121a3c9e3254918f8b171bb7f675b0bdc532244`.
CI completed on 22 October 2026; report consolidated on 23 October. Only Blackspirits/Filee is changed.

[CI run 39800321618](https://github.com/Blackspirits/Filee/actions/runs/39800321618) completed successfully:
all nine jobs passed. This closes the previously pending automated CI and full-formatter checks. It does not
certify an interactive installation, OS-language session, permission dialog or screen reader.

## Native engine-setup follow-up - 24 October 2026

A fresh isolated Windows launch after submission exposed a real pt-PT cut-off in the first-run engine chooser:
the long Calibre name placed `Encontrado neste computador` beyond its card. A deterministic geometry regression
reproduced it (579 px inside a 508 px card); English and pt-BR passed the same baseline case. Package names and
status badges now wrap onto another line without shortening translations or changing window sizes. The three
regression cases, seven first-run render cases, three short-screen/button/escape cases and two UI-scale cases
all pass: **15 / 0 failed / 0 skipped**, 14 seconds. Changed-test whitespace formatting passes. The solution was
rebuilt after the XAML change: zero errors, five existing warnings. Existing demo scenes do not show this chooser,
so their media are unaffected. The 292 App and 176 Setup results below precede this narrow follow-up and are
not added to these fifteen as a unique-test count.

The installed Computer Use skill operated the submitted development app with a new temporary FILEE_DATA_DIR:
automatic language opened in pt-PT, `Página inicial` / `Apoiar` were present, and the Theme page exposed the new
`Escala da interface` controls. Real Ctrl+numpad-plus changed 100% to 110%; Ctrl+0 restored 100% and disabled Repor.
After the fix, a second fresh native profile showed the complete Calibre name and system-copy badge on separate
lines, matching the corrected geometry. Both isolated --quit commands completed with exit code 0. This is native
input evidence, separate from headless captures.
No engine was installed, and no screen-reader, OS-language reversal, UAC or permission result is inferred.
Current source-head/CI status remains in the PR body, with native limitations below still explicit.

## Submission validation - 24 October 2026

The submission integrates upstream `984475954a139fa8beee0ca3e7b6b4c8375ef0f2` in merge `7bdbb3d1`.
All sixteen catalogs contain 1,048 keys: 1,025 upstream entries, nineteen descriptive engine names and four
compact donut labels. All fourteen latest source keys and the changed Wayland message are translated in pt-PT;
the seven earlier corrections remain the only edits to existing upstream pt-BR values. Turkish community
values and layout/order are retained, adding only the established fallback/compact keys. The shared language
demo was regenerated using the existing explicit generator for all sixteen languages: 768 x 432, sixteen
decoded frames, 191,182 bytes. Fifteen total WebP assets occupy 3,900,634 bytes and remain within every budget.
App and Setup were rebuilt afterwards from the faithful temporary source copy; zero errors and five existing
obsolete-API warnings in the final incremental build (fourteen warnings in the preceding integration build).

The final focused App selection passed **292 / 0 failed / 0 skipped** in 3 minutes 37 seconds: 165 localization
cases, 54 Portuguese minimum-width cases, 51 slideshow/media cases, eleven UI-scale cases, three print-studio
cases and eight PDF/FITS editor cases. Headless PNGs are distinct from native interaction; the pt-PT FITS capture
was inspected and its new controls are complete. The complete Setup suite passed **176 / 0 failed / 0 skipped**
in 41 seconds. This does not certify native OS detection, screen readers or
conversion quality. The earlier UI-scale/PDF-table integration also passed all 22 PDF-table engine tests.

Commit `e7101aba` disposes ten remaining directly constructed MainWindowViewModels in RenderTests, releasing
locale/update subscriptions. A 42-case cleanup/zoom selection passed 41 cases and failed the engine-page fixture:
the external Calibre installed on this host returns no version, while the existing assertion requires a version
for every available engine. An isolated repeat and the same fixture before the disposal cleanup both fail at the
same assertion. Its CalibreConverter implementation intentionally reports a null version for external copies.
The exact final source was restored and rebuilt after that baseline comparison. No assertion, production engine
behavior or skip was changed to conceal it. This host failure is separate from translation correctness; the new
complete CI remains required. No measured CI speedup is claimed from the cleanup.

The local SDK 10.0.204 formatter reports two existing whitespace diagnostics at LocalizationService.cs:97-98;
those lines are byte-for-byte upstream. No diagnostic was reported for the four other included C# files. The
complete CI formatter is pending for this submitted head. `git diff upstream/main --check` passes. A whitespace
warning against the older local HEAD comes from an unchanged upstream Preset.cs EOF, outside the contribution.

Previous run [40005326117](https://github.com/Blackspirits/Filee/actions/runs/40005326117), on `478315c3`, ended
cancelled: seven jobs succeeded, macOS Intel and Linux ARM64 hit the 60-minute limit while App tests were still
passing, and the subsequent ARM package check failed without its archive. It is not an all-green result.
Upstream itself increased portable-job timeouts to 90 minutes in the integrated baseline; this contribution
adds no extra timeout increase. Current run status and PR readiness belong in the PR checks/body, avoiding a
report-only source push that restarts the entire matrix.

The earlier real Windows language-switch/restart evidence below remains historical and partial. No new native
claim is made for this FITS/CAD/GNOME integration. A reverse pt-BR OS session, mismatched native number region,
real installation/UAC/Explorer, macOS Finder/permissions, NVDA/VoiceOver and modal keyboard behavior remain
explicit unvalidated follow-ups. They do not indefinitely prevent submission of this translation contribution
under the contributor's authorization. No themes or Phase 2 code is included.

Commands in the verified temporary copy (eighteen pinned fonts):

```powershell
$env:FILEE_WHATSNEW_LANGUAGE='en'
$env:FILEE_WHATSNEW_ONLY='languages'
./tests/Filee.App.Tests/bin/Release/net10.0/Filee.App.Tests.exe -method Filee.App.Tests.WhatsNewMedia.Generate_2_0_0 -explicit only
dotnet build Filee.slnx -m:1 -c Release --no-restore -p:UseSharedCompilation=false
$env:FILEE_LOCALIZATION_SCREENSHOTS='1'
dotnet test tests/Filee.App.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Localization|FullyQualifiedName~PortugueseRenderTests.Pages_render_at_minimum_width_in_both_themes|FullyQualifiedName~Translated_print_settings_fit_at_the_minimum_window_width|FullyQualifiedName~UiZoomTests|FullyQualifiedName~WhatsNewTests|FullyQualifiedName~Preset_editor_shows_and_saves_the_fits_options|FullyQualifiedName~Preset_editor_shows_and_saves_watermark_page_numbers_and_password'
dotnet test tests/Filee.Setup.Tests -c Release --no-build --no-restore
dotnet format Filee.slnx --verify-no-changes --no-restore --severity warn --include src/Filee.App/Services/LocalizationService.cs src/Filee.Setup/SetupOptions.cs tests/Filee.App.Tests/RenderTests.cs tests/Filee.App.Tests/UiZoomTests.cs tests/Filee.App.Tests/SpreadsheetPrintStudioTests.cs
git diff upstream/main --check
```

Logs, TRX and selected PNG evidence are retained in ignored `artifacts/portuguese-phase1/submission/`.

## Final upstream catch-up — 23 October 2026

Merge `614cb4d5` incorporates upstream `a97a07cd2eea20dbf80f03332d2710b874eb09f9`. All 47 newly added
messages are translated in pt-PT. Each of the fifteen catalogs now has 1,025 keys; both Portuguese catalogs
preserve English key order, format-item multiplicity, line breaks and NFC. The seven earlier targeted changes
remain the only edits to existing upstream pt-BR values. Other upstream catalog values/layouts are retained.

App/Setup were rebuilt from the faithful temporary source copy: zero errors, five existing obsolete-API warnings
in the final incremental solution build (the first integration build reported fourteen upstream warnings).
A 179-case App selection passed 176 localization/PDF/preset cases and exposed a pt-BR print-studio caption
cut-off. A preceding measurement had also exposed its workbook choice. Neither community translation was
shortened: the four choice templates and page-width/height captions now wrap. After rebuilding, all three
print-studio geometry cases passed in English, pt-PT and pt-BR at the existing 1,040 px minimum width. These
three final cases replace the earlier studio results and are not added twice. No remaining selected failure
was left unresolved. The fixture's initial double-dispose cleanup was corrected without changing production
lifecycle behavior or weakening assertions. Fresh headless studio PNGs were inspected separately from native
interaction; these tests do not certify LibreOffice export or PDF preview correctness.

The earlier 373-case App/Setup/Core/CLI/Platform results below describe the preceding integration. The final
catch-up's complete native platform matrix is a current CI check, not inferred from those older passes.
Run `39997779173` tested `fd91193` before this catch-up and must not certify the final head. Current run status
and submission readiness are recorded in the PR checks/body so a report-only push does not restart the matrix.
Existing native limitations below remain explicit follow-ups. Themes and Phase 2 have not started.

Commands used in the same verified temporary copy (with eighteen pinned Noto fonts):

```powershell
dotnet restore Filee.slnx --disable-parallel
dotnet build Filee.slnx -m:1 -c Release --no-restore -p:UseSharedCompilation=false
$env:FILEE_LOCALIZATION_SCREENSHOTS='1'
dotnet test tests/Filee.App.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Localization|FullyQualifiedName~Translated_print_settings_fit_at_the_minimum_window_width|FullyQualifiedName~Preset_editor_shows_and_saves_watermark_page_numbers_and_password|FullyQualifiedName~Translated_choices_fit_in_the_preset_dialog'
# After the wrapping correction and rebuild: all three affected geometry cases pass.
dotnet test tests/Filee.App.Tests -c Release --no-build --no-restore --filter 'FullyQualifiedName~Translated_print_settings_fit_at_the_minimum_window_width'
dotnet format Filee.slnx --verify-no-changes --no-restore --severity warn --include tests/Filee.App.Tests/SpreadsheetPrintStudioTests.cs tests/Filee.App.Tests/RenderTests.cs
git diff --check
```

Local logs/TRX/PNG evidence is retained in ignored `artifacts/portuguese-phase1/submission/`.

## CLI argument-description clarification — 23 October 2026

After the contributor clarified the earlier wording decision, pt-PT's `watch.cli_hint` is now
`Também na linha de comandos: filee watch <pasta> --to <formato>`. pt-BR already uses `<pasta>` / `<formato>`;
Spanish, Korean, Russian, Thai and both Chinese catalogs also translate these descriptions. `filee watch` and
`--to` remain the literal syntax recognized by `CliOptions`. The user replaces the angle-bracket descriptions
with a path and format; the application does not interpolate them like `{0}`.

Only this pt-PT catalog value and the corresponding review notes change. All other catalogs are unchanged,
and all 978 pt-PT keys, tokens, line breaks, NFC and English key order are retained. No demonstration contains
this Watch-page footer, so no media regeneration is needed. App and Setup resources were rebuilt in the same
verified temporary copy described below: zero build errors, five existing warnings. The existing App selection
passed **163 / 0 failed / 0 skipped**: 157 localization cases and six Watch-page light/dark/2x geometry cases
across the two Portuguese variants. The Watch cases also emitted fresh headless PNGs. No new native-interaction
claim is made for this wording change. `git diff --check` passes.

Commands:

```powershell
dotnet build Filee.slnx -m:1 -c Release --no-restore -p:UseSharedCompilation=false
$env:FILEE_LOCALIZATION_SCREENSHOTS='1'
dotnet test tests/Filee.App.Tests -c Release --no-build --filter 'FullyQualifiedName~Localization|(FullyQualifiedName~PortugueseRenderTests.Pages_render_at_minimum_width_in_both_themes&DisplayName~watch)'
```

Local evidence is retained in ignored
`artifacts/portuguese-phase1/cli-hint-followup/`. The broader results below describe the preceding integration.

## Current upstream integration — 23 October 2026

UI/media snapshot: `c59c394`; catalog layout preservation: `4fefecf`. The final acknowledgement fixture is
validated separately below; later report-only edits do not change production code or resources.
Upstream `main` was checked again at `4c3a3e1cbedec48c3033c68fd0da161977e7afdf` before publication; the fork's
remote contribution head was `987c03f`, and its main remained `a121a3c`. Published history was preserved by merge
`47616f7`; all seventeen conflicts are resolved. Each of the fifteen catalogs has 978 keys. pt-PT adds 67 new
upstream strings, updates three changed source messages and applies the two approved editorial suggestions.
The final CLI-hint follow-up translates the argument descriptions to `<pasta>` / `<formato>` while retaining
`filee watch` / `--to`. pt-BR retains all newer upstream translations and the seven existing
contribution corrections; no further Brazilian editorial changes were introduced.

The previous [CI run 39987984354](https://github.com/Blackspirits/Filee/actions/runs/39987984354) passed formatting,
build and the App suite, but its Windows job failed one Setup assertion: zh-CN expected browser.webp and saw
flip.webp after the six-second slide timer elapsed. Commit `fea4fa0` freezes automatic advance and selects slide
zero before checking its localized media. The assertion is retained, and the separate automatic-advance test
still checks the six-second interval and slide sequence. That old run is not an all-green result. Current CI
must be read from this fork PR's Checks tab for the published head, not inferred from historical runs.

### Current automatic checks

Windows x64 build 26300.9711, SDK 10.0.204/runtime 10.0.8, eighteen pinned SHA-256-verified Noto fonts.
The checkout refused compiler XML output and the native app's temporary settings write. A faithful copy of the
current branch's tracked files and fonts in the host temporary directory was therefore used for builds, tests,
media generation and the isolated app profile. No security settings, assertions or output helpers were modified.
App/Setup were rebuilt after the nine clips and manifest were regenerated. The final incremental Release build
has zero errors and five existing obsolete-API warnings (the initial clean build reported thirteen warnings).
The temporary sources and final resources are compared byte-for-byte with the contribution before publication.
The six later catalog-format edits preserve values exactly and restore each upstream language's own layout/order;
App/Setup were rebuilt again, then the 162 affected resource checks passed.

| Check | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Focused App localization/layout/theme/palette/slideshow contracts | 373 | 0 | 0 |
| Additional supporter acknowledgement renders (en / pt-PT / pt-BR) | 3 | 0 | 0 |
| Complete Setup suite after final resource embedding | 170 | 0 | 0 |
| Complete Core suite | 532 | 0 | 3 |
| Complete CLI suite | 27 | 0 | 1 |
| Complete Platform suite | 60 | 0 | 5 |

The 373-case App run took 8 minutes 29 seconds. The three additional supporter cases assert that the title
fits in both the support window and About with fixture data, and save separate Portuguese captures. A final
162-case localization/resource/media run passed after preserving per-language catalog whitespace/order and
rebuilding; it overlaps the 373 cases and is not counted again. The preliminary 152 localization passes also
overlap the final App selection. Setup also
passed all 170 cases before the final media rebuild; only the final run is counted above. All three explicit
media-generation runs passed and are excluded from the test total. Platform/dependency skips remain visible in
TRX. This is a focused App selection, not a local full-solution test result; the Engines suite is delegated to CI.
Changed-file formatting (including the acknowledgement fixture) and `git diff --check` pass. Full local formatting exits 2 with whitespace diagnostics in
seventeen files under SDK 10.0.204; fifteen are byte-for-byte upstream files, and the diagnosed lines in the two
localization files are also unchanged upstream text. The current CI formatter remains the separate check.

### Current captures and interaction

All fifteen shared/Portuguese WebP files decode with valid poster indices. The nine regenerated clips are
768 × 432; browser/flip retain their complete shared 640 × 360 desktop recordings. The full media set totals
3,882,212 bytes; every file is below 400 KiB, each pack below 2 MiB and the total below 6 MiB. Both theme posters
are frame 24. The shared language showcase contains all fifteen registered languages. Headless PNGs and decoded
posters are separate from native Computer Use snapshots.

Native Computer Use was available and used with the isolated development profile. The current integrated app
opened automatically in pt-PT, displayed `Apoiar o Filee`, the new Theme descriptions/pastel names and complete
palette tabs/actions. Choosing pt-BR live, Alt+F4 → `Sair`, full process exit 0 and restart without a language
argument retained pt-BR; the General picker and saved JSON agreed. Home/Enter in the picker restored automatic
pt-PT, and JSON contained `auto`. The final isolated `--quit` also exited 0. The restart used the rebuilt final
resources. The acknowledgement is conditional on supporter data and was not visible in this isolated native
session; its wording is verified by glossary and headless supporter fixtures.

The OS session still has pt-PT display and numeric region; native scale was not independently measured. The older
Tab/Shift+Tab/preset-arrow evidence below remains historical, while picker Home/Enter was repeated now. No reverse
pt-BR OS session, mismatched native numeric region, real install/update/uninstall, Explorer/UAC, macOS Finder,
permissions, NVDA or VoiceOver result is claimed. Modal keyboard limitations of the helper remain disclosed.
Per the contributor's decision, unavailable sessions are follow-ups for maintainer review, rather than an
indefinite submission gate. Keep draft while current automated checks or concrete integration defects remain.
Phase 2 has not started, no PR has been merged and no code has been pushed to the original repository.

Logs/TRX/PNG files are retained under the temporary build root recorded in ignored
`artifacts/portuguese-phase1/merged-build-root.txt`; copied evidence is kept in `artifacts/portuguese-phase1/`.

## Editorial follow-up — 23 October 2026

Earlier application/resource commit: `ff7b349e79e95edf6f97c2223408b70667a91553`.
The remote branch and draft PR #1 were confirmed at `484d6fb` before editing; no older snapshot was restored.
Only Blackspirits/Filee is changed. Phase 2 has not started.

Commit `91174f2` changes only two pt-PT catalog values: `about.sponsor` → `Apoiar` and `sponsor.title` →
`Apoiar o Filee`. `Página inicial`, `patrocinadores`, AO90 and informal `tu` are retained. Both catalogs still
have 911 entries. pt-BR and all other JSON catalogs are byte-for-byte unchanged from `484d6fb`.
The contextual glossary now checks these choices and the existing sponsor-window render fixture includes both
Portuguese variants.

Commit `ff7b349` regenerates pt-PT `theme.webp` and `more.webp`, plus the shared `languages.webp`, whose pt-PT
frame also shows the action. The theme poster moves from frame 28 to 29. No pt-BR recording or browser/flip clip
was replaced. The fifteen WebP assets total 4,106,272 bytes; every file remains below 400 KiB, every pack below
2 MiB and the total below 6 MiB. The generated theme/history posters and the showcase's pt-PT frame were inspected. All fifteen media/poster
entries decoded with valid poster indices; the three regenerated clips are 768 × 432. Shared desktop recordings
retain their existing resolution.

### Automated checks on local Windows

Environment: Windows x64 build 26300.9711 (26H2), .NET SDK 10.0.204/runtime 10.0.8, with all fifteen pinned,
SHA-256-verified Noto fonts. The final Release solution build, after restoring the original test helpers and
rebuilding App/Setup's embedded resources, passed with zero errors and eleven existing warnings.

| Check | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Focused App suite, including Portuguese pages, glossary and sponsor windows | 339 | 0 | 0 |
| Setup, complete suite after redirecting PNG output | 168 | 0 | 0 |
| Core, complete suite | 457 | 0 | 3 |
| CLI, complete suite | 27 | 0 | 1 |
| Platform, complete suite | 50 | 0 | 3 |
| App localization/resource/media checks after the final embedded-resource rebuild | 155 | 0 | 0 |

The last row overlaps the 339 cases and is not an additional unique-test count. The broader capture run preceded
the final media embedding; the 155-case run checked the final rebuilt assemblies with the original test helpers.
The two explicit media-generation runs also passed and are excluded from the test counts. Platform/dependency
skips are recorded in the TRX files; no failing test was excluded.

This environment initially refused ImageMagick output in the checkout and PNG creation under the test binaries.
The first Setup run had 150 passes and 18 screenshot-write failures. Output-only, temporary changes routed the
existing media generator and App/Setup screenshot helpers to temporary folders; all assertions remained active.
Those helper changes were restored exactly before the final build and are absent from the commits. Intermediate
build attempts also encountered locks while Filee/the App runner was open; the final build succeeded after both
had exited. The final tests do not depend on the temporary output adapters.

Full local `dotnet format` exits 2 with whitespace diagnostics in seventeen files unchanged from `484d6fb`.
Changed-file formatting (including the acknowledgement fixture) and `git diff --check` pass. The new native CI's complete formatter step has passed;
the local SDK differs from the SDK used by CI, so the local full-format result is not reported as green.

[New CI run 39985961697](https://github.com/Blackspirits/Filee/actions/runs/39985961697) tests `ff7b349`.
At this update it is still in progress: formatting and both Linux native-dependency jobs have passed.
The historical nine-job success on `9d357e7` below is not certification of the editorial head.

Local logs, TRX and headless captures are kept in ignored `artifacts/portuguese-phase1/`, including
`build-final.log`, `Filee.App.Tests.log`, `Filee.App.Tests-final-resources.log`, `Filee.Setup.Tests-retry.log`,
`headless-app/` and `headless-setup/`. These files are not published to the repository.

### Interactive Windows evidence

Computer Use was checked before UI work. The real Release development app ran in its existing isolated
`FILEE_DATA_DIR` mode, preserving the user's normal settings and system registrations. OS display language and
number/date region were both pt-PT; only pt-PT is installed in the session's user language list. The native window
capture was 1082 × 792 pixels; desktop scale was not independently measured. Automated 1x/2x geometry checks
are separate evidence and do not establish the native desktop scale.

The initial native launch used the editorial assets matching `91174f2`; the restart used the rebuilt media head
`ff7b349`. Observed using native UI snapshots/accessibility trees in this session:

- Automatic language opened in pt-PT with `Página inicial` and `Apoiar`.
- Choosing Português (Brasil) live changed the interface to `Início`, `Patrocinar`, `Ligado` and `Desligado`.
- Alt+F4 opened the Brazilian close dialog; choosing `Sair` ended the process with exit code 0. Reopening the same
  profile without `--language` retained pt-BR, including in the General picker. Its saved JSON also contained pt-BR.
- Opening the picker, pressing Home and Enter returned to automatic and restored pt-PT; saved JSON contained `auto`.
- Tab/Shift+Tab showed visible focus and scrolled a General-page action into view. A Down arrow in the preset list
  selected JPG and updated the editor. The native tree exposed the localized `Duplicar` and `Eliminar` button names.
- The real `Apoiar o Filee` window displayed the new title and complete support rows; About also displayed `Apoiar`.
  The native slideshow opened with its Portuguese title, caption and `Seguinte` action.
- The isolated native `--quit` command completed with exit code 0.

These native captures are distinct from the Skia PNG files. Modal keyboard checks could not be completed reliably:
the helper enumerated the owner window and key input refocused that owner, so Right in the slideshow and Escape in
the close dialog are not certified. No screen-reader result is inferred from an accessibility tree. No claim is made
for a pt-BR Windows display session, a mismatched native numeric region, real installation/update/uninstall,
Explorer registration, UAC, macOS/Finder, permissions, NVDA or VoiceOver. W1 and A1 therefore remain only partial.
These limitations remain disclosed; the current submission policy is recorded above.

## Native test evidence (historical CI)

The numbers below are passing tests, taken from each job's final test summaries. No cross-platform sum is
presented as a unique-test count. Platform/dependency skips remain visible in the linked logs; the portable
workflow's existing filter excludes two Windows Explorer import/registration tests on Unix.

| Test project | Windows x64 | Linux x64 | Linux ARM64 | macOS Intel | macOS ARM64 |
| --- | ---: | ---: | ---: | ---: | ---: |
| App | 1041 | 1033 | 1033 | 1032 | 1032 |
| Core | 458 | 460 | 460 | 458 | 458 |
| Platform | 50 | 49 | 49 | 48 | 48 |
| Engines | 1391 | 1376 | 1374 | 1376 | 1376 |
| CLI | 27 | 28 | 28 | 27 | 27 |
| Setup | 168 | not run | not run | not run | not run |

Every listed run has zero failed tests. Setup's full Windows suite passed all 168 cases; the 36 failures reproduced
in the earlier Linux baseline comparison are not Windows failures. The Windows installer check also completed
installation, update and uninstallation using its existing stub-app/scratch-registry sandbox.

The App run took 21.9106 minutes on Windows, 16.2564 on Linux x64, 29.7618 on Linux ARM64, 27.1291 on macOS Intel
and 24.8293 on macOS ARM64. Intel macOS finished within the existing 60-minute job limit. This is one successful
measurement after the fixture-disposal/render-matrix changes, not proof of a long-term performance trend.

Sources:

- [Windows build, formatting, tests and installer](https://github.com/Blackspirits/Filee/actions/runs/39800321618/job/119898834765)
- [Linux x64 tests and package](https://github.com/Blackspirits/Filee/actions/runs/39800321618/job/119899039832)
- [Linux ARM64 tests and package](https://github.com/Blackspirits/Filee/actions/runs/39800321618/job/119899040054)
- [macOS Intel tests and package](https://github.com/Blackspirits/Filee/actions/runs/39800321618/job/119899039944)
- [macOS ARM64 tests and package](https://github.com/Blackspirits/Filee/actions/runs/39800321618/job/119899040141)

## Packaging and visual evidence

Both Linux native-dependency jobs passed. The x64 and ARM64 Linux packages also passed the separate Ubuntu 22.04
compatibility jobs. All four portable jobs passed archive/converter checks and the packaged desktop-app startup,
quit and platform-specific smoke checks. The macOS startup check starts the executable directly; it does not
exercise Finder's locale/permission flow.

Both macOS packages passed `plutil -lint` for the pt-PT and pt-BR InfoPlist.strings and ServicesMenu.strings files.
This establishes resource validity and successful packaging, while actual native text selection remains unchecked.

Downloaded CI evidence inspected before this report:

- Windows `ui-screenshots`, artifact 12437246718.
- macOS Intel `unix-test-results-osx-x64`, artifact 12438500463, including its five TRX files.

The macOS TRX files confirm the passing-test counts above. The inspected Windows installer options capture has
complete pt-PT action text; its remaining options are intentionally in a scrollable sheet. Windows pt-PT and macOS
pt-BR crop-slide captures retain their respective Repor/Redefinir, Ignorar/Pular and Seguinte/Próximo wording.
The real macOS desktop capture shows Filee's packaged window opening successfully in the runner's English session.
It must not be used as evidence of Portuguese OS detection.

The broader Portuguese page/dialog geometry and captured layout checks are described in
[the review follow-up](PORTUGUESE-REVIEW-FOLLOWUP.md). PNG output from PortugueseRenderTests is deliberately
opt-in; native CI still runs their geometry assertions. No screenshot or static resource check certifies NVDA,
VoiceOver or the system's permission prompts.

## Remaining interactive checks

These cases remain open. W1 and A1 have only the partial evidence recorded above; no row is fully certified.
Record the tested commit, OS/version,
display language, regional format, scale, result and evidence for each language.

| ID | Environment and action | Expected result | Status |
| --- | --- | --- | --- |
| W1 | Windows with pt-PT display language: start with automatic language, choose pt-BR manually, quit and restart; return to automatic. Repeat with the two display languages exchanged. | Automatic follows the OS display language; a saved explicit variant survives restart; returning to automatic selects the OS variant again. | Partial: pt-PT session → explicit pt-BR → full restart → automatic passed; reverse OS-language session pending. |
| W2 | Repeat each Portuguese UI with a different number/date region; edit a fractional playback value and save/reopen the preset. | Display uses the selected regional separator and the saved numeric value is unchanged. | Pending: native session uses matching pt-PT region; mismatched regions are covered only automatically. |
| W3 | Install the real app in each variant, launch it, update and uninstall; exercise per-user/system installation and an optional engine. | Installer text stays in the chosen variant, the launched app receives the explicit choice, and UAC/Explorer integration behaves normally. | Pending: development copy was isolated; real release installation, Explorer and UAC were not exercised. |
| M1 | macOS with each Portuguese preferred language: launch from Finder and use the conversion service and the normal permissions flow. | Finder's service title and permission explanations use the OS-selected Portuguese variant; the app's automatic language and numeric region remain correct. | Pending: no macOS interactive host in this session. |
| A1 | Navigate the language picker, preset editor, close dialog and slideshow using Tab/Shift+Tab, arrows, Enter and Escape; repeat with NVDA on Windows and VoiceOver on macOS. | All actions are reachable, focus remains visible, names/roles identify their purpose, and no dialog traps focus. | Partial native picker/preset/focus evidence; modal keyboard helper limitation, NVDA and VoiceOver remain open. |

Do not infer these results from successful headless tests. Unavailable native sessions are documented follow-ups
for maintainer review; they do not certify a release-package manual matrix. No upstream write or Phase 2 code
changes are included in this update.
Portuguese browser/flip desktop recordings remain an explicitly documented media follow-up.

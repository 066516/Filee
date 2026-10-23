# Portuguese native validation

Historical CI-validated code commit: `9d357e7bcb6fd389d2fa8e48faa1ba3f8c39b13d`.
Baseline: `a121a3c9e3254918f8b171bb7f675b0bdc532244`.
CI completed on 22 October 2026; report consolidated on 23 October. Only Blackspirits/Filee is changed.

[CI run 39800321618](https://github.com/Blackspirits/Filee/actions/runs/39800321618) completed successfully:
all nine jobs passed. This closes the previously pending automated CI and full-formatter checks. It does not
certify an interactive installation, OS-language session, permission dialog or screen reader.

## Editorial follow-up — 23 October 2026

Current application/resource commit: `ff7b349e79e95edf6f97c2223408b70667a91553`.
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
Changed-file formatting and `git diff --check` pass. The new native CI's complete formatter step has passed;
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
The full remaining checklist below still gates leaving draft status.

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

Do not infer these results from successful headless tests. Keep the localization PR in draft until this final
interactive verification is recorded. No merge, upstream PR or Phase 2 code changes are included in this update.
Portuguese browser/flip desktop recordings remain an explicitly documented media follow-up.

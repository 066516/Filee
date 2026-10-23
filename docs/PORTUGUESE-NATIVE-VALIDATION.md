# Portuguese native validation

Validated code commit: `9d357e7bcb6fd389d2fa8e48faa1ba3f8c39b13d`.
Baseline: `a121a3c9e3254918f8b171bb7f675b0bdc532244`.
CI completed on 22 October 2026; report consolidated on 23 October. Only Blackspirits/Filee is changed.

[CI run 39800321618](https://github.com/Blackspirits/Filee/actions/runs/39800321618) completed successfully:
all nine jobs passed. This closes the previously pending automated CI and full-formatter checks. It does not
certify an interactive installation, OS-language session, permission dialog or screen reader.

## Native test evidence

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

These cases are **pending**, not failed and not claimed as tested. Record the tested commit, OS/version,
display language, regional format, scale, result and evidence for each language.

| ID | Environment and action | Expected result | Status |
| --- | --- | --- | --- |
| W1 | Windows with pt-PT display language: start with automatic language, choose pt-BR manually, quit and restart; return to automatic. Repeat with the two display languages exchanged. | Automatic follows the OS display language; a saved explicit variant survives restart; returning to automatic selects the OS variant again. | Pending |
| W2 | Repeat each Portuguese UI with a different number/date region; edit a fractional playback value and save/reopen the preset. | Display uses the selected regional separator and the saved numeric value is unchanged. | Pending |
| W3 | Install the real app in each variant, launch it, update and uninstall; exercise per-user/system installation and an optional engine. | Installer text stays in the chosen variant, the launched app receives the explicit choice, and UAC/Explorer integration behaves normally. | Pending |
| M1 | macOS with each Portuguese preferred language: launch from Finder and use the conversion service and the normal permissions flow. | Finder's service title and permission explanations use the OS-selected Portuguese variant; the app's automatic language and numeric region remain correct. | Pending |
| A1 | Navigate the language picker, preset editor, close dialog and slideshow using Tab/Shift+Tab, arrows, Enter and Escape; repeat with NVDA on Windows and VoiceOver on macOS. | All actions are reachable, focus remains visible, names/roles identify their purpose, and no dialog traps focus. | Pending |

Do not infer these results from successful headless tests. Keep the localization PR in draft until this final
interactive verification is recorded. No merge, upstream PR or Phase 2 code changes are included in this update.
Portuguese browser/flip desktop recordings remain an explicitly documented media follow-up.

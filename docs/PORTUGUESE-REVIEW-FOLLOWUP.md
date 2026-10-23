# Independent Portuguese review: follow-up

The independent review examined b694dad, based on Filee 2.0.0 at 799e394. This follow-up integrates current upstream
a121a3c9e3254918f8b171bb7f675b0bdc532244, verified on 22 October 2026. Only Blackspirits/Filee is changed.

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
and keyboard/NVDA/VoiceOver checks remain required. Complete native CI has since passed all nine jobs; the
[native validation report](PORTUGUESE-NATIVE-VALIDATION.md) records the evidence and interactive checklist.
The PR stays a draft pending those interactive checks.

## References

- [Apple pt-PT automation permissions](https://support.apple.com/pt-pt/guide/mac-help/mchl108e1718/mac)
- [Apple pt-PT Dock](https://support.apple.com/pt-pt/guide/mac-help/mh35859/mac)
- [Apple pt-BR Dock](https://support.apple.com/pt-br/guide/mac-help/mh35859/mac)
- [Reviewed CI run](https://github.com/Blackspirits/Filee/actions/runs/39695610117)

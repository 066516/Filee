# Independent Portuguese review: follow-up

The independent review examined b694dad, based on Filee 2.0.0 at 799e394. This follow-up integrates current upstream
a121a3c9e3254918f8b171bb7f675b0bdc532244, verified on 22 October 2026. Only Blackspirits/Filee is changed.

## Findings and responses

| Finding | Evidence checked and response |
| --- | --- |
| P1-1: upstream conflicts and outdated coordination | Reproduced the original ten conflicts, merged French/Traditional Chinese and later current upstream/Nepali, retaining both Portuguese variants and both sets of tests. Preserve published history with merges. Add missing new resource keys and update coordination notes. |
| P1-2: CI duration | Run 39695610117 had 8/9 successes; Intel macOS hit the 60-minute job limit while individual tests were still passing. This does not prove a single hung test. Three render fixtures failed to dispose directly constructed MainWindowViewModels, leaving service subscriptions alive; they now use using. Reduce 72 minimum-width cases to 54: both themes at 1x and every language/page at 2x light. PNG encoding is opt-in; assertions always run. No timeout is increased and no failed cases are skipped. Full native CI still needs to measure the effect. |
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

## Current validation

Validation uses .NET SDK 10.0.402 on Linux x64 and all fifteen SHA-256-verified pinned Noto files.
The Release solution build succeeds with zero errors and eleven existing warnings.

- Initial App smoke selection: 41 passed, zero failures, including default action bounds in every registered locale.
- Full Setup suite: 131 passed, 36 failed, one skipped. The 36 failure names exactly match a clean upstream a121a3c
  checkout (53 passed / 36 failed in its non-UI suite), using the same Linux environment. Windows-style fixture paths,
  PE and registry/path assumptions remain the cause. No production paths or test exclusions are changed to hide them.
- The broader final App selection and Core/CLI/Platform suites are running; their final counters will be added.
- Full solution formatting cannot connect to the build host pipe here (SocketException 13; host exit 137).
  Changed-file whitespace checks and native CI provide the remaining formatter verification.

Earlier interrupted App runs are not full-suite passes. Real installation/UAC, Finder and permission prompts,
keyboard/NVDA/VoiceOver checks, and full native CI remain required. The PR stays a draft.

## References

- [Apple pt-PT automation permissions](https://support.apple.com/pt-pt/guide/mac-help/mchl108e1718/mac)
- [Apple pt-PT Dock](https://support.apple.com/pt-pt/guide/mac-help/mh35859/mac)
- [Apple pt-BR Dock](https://support.apple.com/pt-br/guide/mac-help/mh35859/mac)
- [Reviewed CI run](https://github.com/Blackspirits/Filee/actions/runs/39695610117)

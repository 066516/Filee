# Portuguese localization

Phase 1 adds European Portuguese and refines regional integration with the Brazilian translation already merged
in #322. The current baseline is upstream `a97a07cd2eea20dbf80f03332d2710b874eb09f9` (23 October 2026).
French, Traditional Chinese, Nepali, Thai, Material You, OCR, palette tabs and the other merged upstream changes
are retained.
Phase 2 has not started.

## Coverage and wording

Both Portuguese catalogs contain all **1,025 English keys**, in English key order: 1,003 upstream entries,
18 descriptive engine names and four compact donut labels. Three native macOS strings per variant are additional
to those JSON entries. There are fifteen registered languages. The twelve non-Portuguese translations retain
English for the new engine names pending maintainer review; compact labels reuse their existing full translations.
This preserves their earlier metadata-based display rather than claiming unreviewed native translations.

European Portuguese follows AO90 and informal `tu`. Brazilian Portuguese preserves the community wording from
#322; only seven existing upstream entries change, correcting count agreement and an ambiguous pointer reference.
Earlier broad Brazilian editorial changes are superseded. Valid Brazilian wording includes `no Dock`, `Gravar`,
`Redefinir` versus `Restaurar`, `Relatar`, `E-books`, `Ligado` and curly quotes. Similarity between variants alone
is not evidence of an error.

| English | pt-PT | pt-BR |
| --- | --- | --- |
| File | Ficheiro | Arquivo |
| Save | Guardar | Salvar |
| Settings | Definições | Configurações |
| Preset | Predefinição | Predefinição |
| Profile | Perfil | Perfil |
| Engine navigation | Motores | Motores |
| Download | Descarregar | Baixar |
| Frame | Fotograma | Quadro |
| Mouse | Rato | Mouse |
| System default | Predefinição do sistema | Padrão do sistema |
| About | Acerca de | Sobre |
| Home | Página inicial | Início |
| Sponsor action | Apoiar | Patrocinar |
| Sponsor dialog | Apoiar o Filee | Patrocinar o Filee |
| Supporter acknowledgements | Obrigado a quem apoia o Filee | Obrigado aos nossos patrocinadores |
| Percent | Percentagem | Porcentagem |
| Mixed files | Ficheiros mistos | Arquivos mistos |

Glossary checks are contextual: `arquivo` is appropriate pt-PT when describing PDF/A archiving, for example.
Tests preserve placeholder multiplicity, numeric format specifiers, newlines and NFC. Actual folder names,
regexes, replacement references and command switches stay unchanged. Quotation style and descriptions such as
`<pasta>` in command examples are translatable; `filee watch` and `--to` are literal syntax. The CLI hint uses
`filee watch <pasta> --to <formato>`, matching pt-BR's translated argument descriptions. Only the user replaces
`<pasta>` and `<formato>` with an actual path and format; these are not runtime interpolation tokens.

## Language and region policy

The application and Windows installer share `AppLanguages.All`, the Portuguese region resolver and JSON resources.
Setup passes its choice using the `--language=` argument the app parses. Existing saved choices remain valid.

| Setting / display language | Result |
| --- | --- |
| Explicit pt-PT on any supported system | pt-PT |
| Explicit pt-BR on any supported system | pt-BR |
| Automatic + pt-BR, pt_BR or pt-Latn-BR | pt-BR |
| Automatic + pt, pt-PT, pt-AO, pt-MZ or pt-CV | pt-PT |
| Automatic + pt-PT-x-br or pt-Latn-PT-x-br | pt-PT |
| Automatic + an unsupported language | English |

Only the region after the language and optional script determines the variant. Extensions/private-use subtags
cannot turn Portugal into Brazil. Canonical codes are case-insensitive; Setup also accepts legacy ptpt/ptbr codes.
The original OS UI culture is captured before the app changes it, and macOS uses its preferred language API.
Explicit valid choices never consult the automatic-language provider. Existing French, Chinese-script and other
language resolution remains covered by upstream tests.

Neutral `pt` → pt-PT is a documented application policy, rather than CLDR likely-subtag inference. Someone who
previously selected pt-BR explicitly keeps it; choose Português (Portugal) in General to use the new variant.

`CurrentUICulture` controls interface language. Number/date display retains the user's regional `CurrentCulture`,
including playback multipliers and frame limits; saved values remain numeric. Count labels use neutral forms such
as `Conversões: 1` and `1 s para terminar`, tested with 0, 1, 2 and 21. This is not a general CLDR plural engine.
macOS Finder regional-culture initialization still needs a native check; it is not claimed fixed here.

## Interface and native resources

Preset choices and field captions wrap at 440/560 px; close-dialog actions stay inside their window. Toolbar,
Theme, Presets and Engines keep usable columns with local scrolling at the existing 820 px main-window minimum.
The toolbar content minimum is 786 px, fitting the default 1080 px window after navigation and margins. Its new
preset/group actions wrap onto another row instead of cutting translated labels. Default-width tests cover all
four editors in English/Portuguese and toolbar actions in every registered language, including RTL.

Compact donut labels are `Tamanho 50%`, `Vetorizar`, `Traços` and `Extrair vídeo`. Runtime and editor preview use
the same labels; palette and preset-editor names remain complete, and custom names retain priority. The upstream
caption-wrapping fix is retained. Two icon-only preset actions now have explicit localized automation names.

macOS declares pt-PT and pt-BR and ships ServicesMenu.strings plus InfoPlist.strings for both. The native service
title matches the JSON catalog; permission explanations distinguish ficheiros/utilizas/deteta from
arquivos/você/detecta. Packaging validates strings with plutil and copies every lproj directory. The system chooses
native strings using its language preferences, separately from the app's live JSON language selection.

`Convert with Filee` remains the native service identifier. `Filee Donut` remains the KWin window identifier.
Neither is translated; tests check their stability and resource consistency.

## Demonstration media

Formats, crop, theme and more have recordings in both variants: eight localized WebP files. Browser and flip
retain the shared upstream desktop recordings because their earlier Portuguese headless replacements omitted
important browser/file-manager context. Proper Portuguese desktop recordings remain a media follow-up; their slide
titles and explanations are localized. The multilingual language showcase also remains shared.

`localizedMedia` selects an exact language and its own poster frame; pt-PT never borrows pt-BR. Invalid paths are
ignored. App and Setup refresh the selected media on a language change without resetting the current slide.
Every file stays under 400 KiB, each pack under 2 MiB and all shared/Portuguese media under 6 MiB.

## Earlier editorial follow-up — 23 October 2026

`about.sponsor` is now `Apoiar` and `sponsor.title` is `Apoiar o Filee` in pt-PT. `Página inicial` and the
acknowledgement `patrocinadores` remain unchanged; no pt-BR or other JSON catalog was edited in this follow-up.
The existing glossary and sponsor-window render cases cover both variants. pt-PT theme/history clips and the
shared language showcase were regenerated; App and Setup were rebuilt afterwards.

The [earlier validation report](PORTUGUESE-NATIVE-VALIDATION.md#editorial-follow-up--23-october-2026) separates
local automated results, headless PNGs and partial real Windows interaction. It also links the new CI run;
the historical successful run below must not be treated as validation of the new head. The earlier head remained
a draft. See the current integration report for readiness.

## Final upstream catch-up — 23 October 2026

Merge `614cb4d5` incorporates upstream `a97a07c` without conflicts or rewritten history. The 47 new English
keys cover the spreadsheet print studio, PDF finishing/passwords and update rollback notices. pt-PT translates
all of them; the new upstream pt-BR and other-language values are retained unchanged. The seven earlier Brazilian
count/pointer corrections remain the only changes to existing upstream Brazilian entries.

The new print-studio layout permits translated choices and page-count captions to wrap. Three geometry cases
exercise English and both Portuguese variants at its existing 1,040 px minimum width; the existing PDF-finishing
render cases now include both Portuguese variants. This follows a reproduced pt-BR cut-off without shortening
community wording. Existing Portuguese demonstration scenes do not contain these new controls, so their media
remain valid. App and Setup embed the rebuilt catalogs. The previous integration/test counts below describe
older snapshots; current CI/readiness is reported in the PR checks and the latest native-validation section.

## Current integration and approved final wording — 23 October 2026

Merge `47616f7` integrates upstream `4c3a3e1` without rewriting published history. All seventeen conflicts are
resolved; the current upstream translations are retained key by key. pt-PT gains 67 translations and updates the
three messages whose English sources changed. Thai receives the same 22 contribution-specific fallback/compact
keys as the other catalogs; its upstream translations are preserved.

The contributor approved `Obrigado a quem apoia o Filee` for `sponsor.thanks` and a clearer `merge.order_hint`
explaining that files are combined from top to bottom. `about.sponsor` / `sponsor.title` remain `Apoiar` /
`Apoiar o Filee`, and Home remains `Página inicial`. The glossary case follows the new acknowledgement. pt-BR has
no new editorial rewrite. The eight Portuguese recordings and shared language showcase are regenerated against
the integrated UI, and App/Setup resources are rebuilt afterwards.

The [latest validation report](PORTUGUESE-NATIVE-VALIDATION.md#current-upstream-integration--23-october-2026)
records the current tests, real Windows interaction and the earlier CI timing failure that is fixed separately.
The unavailable OS-language, installation/UAC, Finder/permission and screen-reader sessions remain disclosed
follow-ups for maintainer review. At the contributor's request they do not indefinitely prevent submission of
Phase 1; the release-package manual matrix in CONTRIBUTING.md is separate from an ordinary translation PR.
Draft status remains appropriate while current automated checks or concrete integration defects are unresolved.

## Validation and remaining checks

[Independent-review follow-up](PORTUGUESE-REVIEW-FOLLOWUP.md) records current commands, results and the disposition
of each finding. Earlier test counts describe previous snapshots and must not be used as certification of this head.
Set FILEE_LOCALIZATION_SCREENSHOTS=1 for PNG captures in PortugueseRenderTests; geometry assertions always run.
All eighteen pinned Noto font files, including the new upstream Thai fonts, are used for the visual checks.

[Native CI validation](PORTUGUESE-NATIVE-VALIDATION.md) records run 39800321618: all nine jobs passed on the
validated code commit 9d357e7, including full formatting, the Windows installer suite, portable packages and
native startup checks. The following interactive checks remain open and must not be reported as passed:

- Windows pt-PT/pt-BR display language with automatic selection, explicit opposite choice and restart; repeat with
  a different number/date region.
- Real installation, update, launch and uninstall in both variants, including UAC and optional engines. The CI
  installer sandbox uses a stub app and scratch registry and does not certify that interaction.
- macOS started from Finder with each preferred Portuguese language; check service titles and permission prompts.
- Keyboard navigation, focus, names and roles with NVDA and VoiceOver. Headless captures cannot certify those tools.

The separate CLI provider and raw exception details remain English. A CLI-localization design must preserve JSON,
command names and identifiers; it is deferred to Phase 2. General security, conversion quality, performance,
pre-existing accessibility gaps and dependency work also belong to that separate phase.

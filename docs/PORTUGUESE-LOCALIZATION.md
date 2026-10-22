# Portuguese localization

Phase 1 adds European Portuguese and refines regional integration with the Brazilian translation already merged
in #322. The current baseline is upstream `a121a3c9e3254918f8b171bb7f675b0bdc532244` (22 October 2026).
French, Traditional Chinese, Nepali, KWin, caption wrapping and the other merged upstream fixes are retained.
Phase 2 has not started.

## Coverage and wording

Both Portuguese catalogs contain all **911 English keys**, in English key order: 889 existing entries,
18 descriptive engine names and four compact donut labels. Three native macOS strings per variant are additional
to those JSON entries. There are fourteen registered languages. The eleven non-Portuguese translations retain
English for the new engine names pending maintainer review; compact labels reuse their existing full translations.
This preserves their earlier metadata-based display rather than claiming unreviewed native translations.

European Portuguese follows AO90 and informal `tu`. Brazilian Portuguese preserves the community wording from
#322; only seven of its 889 existing entries change, correcting count agreement and an ambiguous pointer reference.
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
| Percent | Percentagem | Porcentagem |
| Mixed files | Ficheiros mistos | Arquivos mistos |

Glossary checks are contextual: `arquivo` is appropriate pt-PT when describing PDF/A archiving, for example.
Tests preserve placeholder multiplicity, numeric format specifiers, newlines and NFC. Actual folder names,
regexes, replacement references and command switches stay unchanged. Quotation style and descriptions such as
`<pasta>` in command examples are translatable; `filee watch` and `--to` are literal syntax.

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

## Validation and remaining checks

[Independent-review follow-up](PORTUGUESE-REVIEW-FOLLOWUP.md) records current commands, results and the disposition
of each finding. Earlier test counts describe previous snapshots and must not be used as certification of this head.
Set FILEE_LOCALIZATION_SCREENSHOTS=1 for PNG captures in PortugueseRenderTests; geometry assertions always run.
All fifteen pinned Noto font files are used for the visual checks.

The PR remains a draft until complete native CI and the following desktop checks are closed:

- Windows pt-PT/pt-BR display language with automatic selection, explicit opposite choice and restart; repeat with
  a different number/date region.
- Real installation, update, launch and uninstall in both variants, including UAC and optional engines. The CI
  installer sandbox uses a stub app and scratch registry and does not certify that interaction.
- macOS started from Finder with each preferred Portuguese language; check service titles and permission prompts.
- Keyboard navigation, focus, names and roles with NVDA and VoiceOver. Headless captures cannot certify those tools.

The separate CLI provider and raw exception details remain English. A CLI-localization design must preserve JSON,
command names and identifiers; it is deferred to Phase 2. General security, conversion quality, performance,
pre-existing accessibility gaps and dependency work also belong to that separate phase.

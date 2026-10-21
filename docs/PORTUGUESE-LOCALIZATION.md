# Portuguese localization review

Reviewed on 21 October 2026 against upstream `259c5ecd83c57ed155bbe475f1a5315f2a1b5ef6`.
The first implementation used `c63a261`, then integrated the installer at `85f7e56` on 20 October.
This report covers Phase 1. Security, conversion fidelity and performance still require the separate Phase 2 audit.

## Coverage and editorial review

Both Portuguese catalogs contain all **907 English keys**, in English key order: upstream's 889 entries and
18 descriptive engine names that previously came directly from C# metadata. Those 18 keys are translated in
all eleven registered languages, including the Arabic catalog introduced upstream during this work.

The second review compared every English/pt-PT/pt-BR entry and its interface context. It refined 47 existing
pt-PT entries and 51 pt-BR entries, in addition to translating 38 new upstream messages and updating six changed
messages. New coverage includes the crop interface, PDF merge ordering, Live/Motion Photos, subfolder policy,
AV1 acceleration and the revised installer text. These features come from merged upstream code.

European Portuguese uses AO90 and informal `tu`; Brazilian Portuguese uses `você` and its corresponding
imperatives. Corrections include `respetivo`, `gestor de arquivos`, `Guarda` and `ao abrigo` carried into pt-BR.
The pt-PT review clarifies filename-pattern inheritance, sharing quality, geospatial coordinates and missing
runtime components. Built-in engine names explicitly say `motor integrado` / `mecanismo integrado`, avoiding
apparent agreement errors such as `Legendas (integrado)`.

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
| System default | Predefinição do sistema | Padrão do sistema |
| About | Acerca de | Sobre |
| Percent | Percentagem | Porcentagem |
| Small (for sharing) | Tamanho reduzido (para partilhar) | Tamanho reduzido (para compartilhar) |
| Fonts (built-in) | Tipos de letra (motor integrado) | Fontes (mecanismo integrado) |

Presets store reusable conversion settings; profiles choose the toolbar for a file selection. These concepts keep
different names. Regional checks are contextual: `arquivo` is correct pt-PT when describing PDF/A archiving, for
example, so a blanket word ban would be wrong. A twelve-entry glossary regression test protects important
variant boundaries. No remaining cross-variant wording was identified in this review; automated checks alone
cannot establish editorial perfection.

Existing keys, product names, format identifiers, placeholders, newline counts and filename tokens are preserved.
The literal `Photo_` in the rename recipe matches the actual replacement. Likewise, `originals`, `converted`,
`NAME.MP.jpg`, `IMG_(\d+)`, `$1`, CLI switches and third-party technical notices retain their technical meaning.

## Integration and region policy

`LocalizationService` publishes JSON strings as dynamic Avalonia resources. English remains the runtime
fallback; an unrecognized key returns the key itself. Code-generated pages rebuild on `LanguageChanged`.
The Windows installer embeds the same catalogs and shares `AppLanguages.All` and its Portuguese region policy.
Its chosen language is passed through the `--language=` argument that Filee actually parses. No settings migration
or extra Portuguese font is needed.

| Setting / OS display language | Result |
| --- | --- |
| Explicit `pt-PT`, including on a Brazilian system | pt-PT |
| Explicit `pt-BR`, including on a Portuguese system | pt-BR |
| Automatic + `pt-BR`, `pt_BR` or `pt-Latn-BR` | pt-BR |
| Automatic + `pt`, `pt-PT`, `pt-AO`, `pt-MZ`, `pt-CV` | pt-PT |
| Automatic + `pt-PT-x-br`, `pt-x-br`, `pt-Latn-PT-x-br` | pt-PT |
| Automatic + unsupported language | English |

Canonical language codes are case-insensitive. Setup also accepts legacy `ptpt` / `ptbr` installer codes.
The resolver reads the region after the language and optional script, before extensions/private-use subtags.
A later `br` must not change a Portuguese region to Brazil. The same policy is tested in App and Setup.

The merged upstream resolver captures the user's initial UI culture, rather than Windows' installation language,
and uses macOS's preferred language when available. Explicit choices never consult the OS provider. This
preserves upstream's automatic-language fix and Arabic/RTL support alongside both Portuguese variants.

Changing the UI language changes `CurrentUICulture`, while number/date display retains the user's regional
`CurrentCulture`. Playback multipliers and frame-count limits respect it too; serialized conversion settings stay
numeric. Tests include Portuguese UI with US regional formatting. Count summaries use neutral labels such as
`Conversões: 1` and `1 s para terminar`; zero, one, two and twenty-one are checked. This is not a general CLDR
plural engine; future messages needing grammatical plural categories should add an explicit plural contract.

## Reproduced findings

P0 = critical, P1 = high, P2 = medium, P3 = low. This localization review makes no P0/P1 claim about the wider app.

| Priority | Evidence and reproduction | Correction |
| --- | --- | --- |
| P2 | Portuguese close-dialog actions exceed the 440 px window. Bounds tests failed before the first fix. | Wrap the actions; test each action and its text inside the window. |
| P2 | At the existing 820 px main-window minimum, fixed editor columns overlap or become unusably narrow. | Give Toolbar, Theme, Presets and Engines usable content widths with local horizontal scrolling. Reapply the guard around current upstream controls, including the updated toolbar extension editor. |
| P2 | Open the preset dialog at 440/560 px. Quality, codec and destination choices are cut off; all fifteen new choice-bound cases failed before wrapping. The PDF DPI caption also clips at 440 px. | Wrap choice text and field captions inside the preset editor. Keep format names' deliberate ellipsis behavior. Test image, PDF and video presets, plus an English regression case. |
| P2 | Playback speeds and frame limits use invariant separators despite a Portuguese regional culture. | Use `CurrentCulture` for display and verify that applying the option saves the same numeric value. |
| P2 | Descriptive engine names bypass localization through `Converter.DisplayName`. | Add 18 resource keys in every catalog, with metadata fallback for unknown engines; preserve brands. |
| P2 | The two icon-only preset actions lack explicit localized accessible names. | Bind automation names to existing translation keys and verify both variants. Native screen-reader behavior remains to be checked. |
| P3 | `pt-PT-x-br` and three related cases resolve to pt-BR because any later `BR` subtag is treated as a region. Four new resolver cases failed before the correction. | Interpret the region position, retaining script and legacy-code handling. Both App and Setup cover eight additional region/extension cases. |
| P3 | A separate test language list can omit newly registered catalogs; duplicate JSON keys are silently overwritten. | Use the shared registry and verify file parity, unique keys, non-empty values and placeholders. Retain upstream's new registry/Arabic coverage rather than adding a competing list. |

Portuguese tests compare placeholder multiplicity, named tokens, newline counts and Unicode NFC normalization,
and parse/format every numeric composite format. The global set-based placeholder check remains: other languages
can legitimately avoid repeating an argument.

## Validation

Recorded on Linux x64 with .NET SDK 10.0.402 and all **15 pinned Noto font files**, each SHA-256 verified:

| Check | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Release solution build (14 projects) | — | 0 errors | — |
| App localization, Portuguese UI, crop, merge-order and preset contracts | 261 | 0 | 0 |
| Setup texts, UI states and language handoff | 63 | 0 | 1 |
| Core, full suite | 456 | 0 | 0 |
| CLI, full suite | 28 | 0 | 0 |
| Platform, full suite | 46 | 0 | 5 |

These checks total **854 passed tests and 6 platform-specific skips**. The build reports ten existing warnings.
The Windows-only installer-path assertion stays enabled on Windows. A final editor-caption adjustment is followed
by a focused rerun of the preset-dialog, media/archive and Live Photo cases: all 29 passed. They are not counted again in that total.

Reproduce the main checks from the repository root after fetching fonts with `build/fetch-fonts.ps1`:

```sh
dotnet build Filee.slnx -m:1 -c Release
dotnet test tests/Filee.App.Tests -m:1 -c Release --filter "FullyQualifiedName~Localization|FullyQualifiedName~Portuguese|FullyQualifiedName~InstallerTests|FullyQualifiedName~Watch_folder_options_rename_and_filter_with_a_preview|FullyQualifiedName~Preset_editor_shows_media_and_archive_options|FullyQualifiedName~One_picture_shows_the_frame_its_size_and_the_aspect_dial|FullyQualifiedName~Several_files_show_a_strip_with_how_each_went|FullyQualifiedName~Chooser_fits_long_paths_at_its_minimum_width|FullyQualifiedName~Live_photo_options_render_for_jpg_and_heic_and_save_only_there"
dotnet test tests/Filee.Setup.Tests -m:1 -c Release --filter "FullyQualifiedName~SetupTextsTests|FullyQualifiedName~SetupWindowTests|FullyQualifiedName~What_setup_hands_to_filee_is_what_filee_reads"
dotnet test tests/Filee.Core.Tests -m:1 -c Release
dotnet test tests/Filee.Cli.Tests -m:1 -c Release
dotnet test tests/Filee.Platform.Tests -m:1 -c Release
```

The full solution formatter cannot connect to Roslyn's build-host pipe here (`SocketException (13): Permission
denied`, host exit 137). Folder-based whitespace verification of all fourteen changed C# files and
`git diff --check` pass. The fork's CI has now started; its remote result must be checked independently.

This is not a clean full-solution test result. Earlier broad engine checks on `c63a261` recorded 1,139 passes,
6 failures and 40 skips; five failures reproduced on an untouched checkout, while the remaining LibreOffice case
passed in isolation. That is historical evidence, not validation of the newer engine code in `259c5ec`. Earlier
unfiltered App runs also did not complete and encountered denied named-pipe sockets. The current completed App
run is the focused selection above. The CLI keeps its separate, English-only text provider.

## Visual and native checks

Headless Skia screenshots cover nine app pages in both variants at the 820 px minimum, light/dark themes and
100%/200% render scaling, including the bottom/right portions of scrollable editors. Additional captures cover
image/PDF/video preset dialogs at 440/560 px, Live Photo options in both Portuguese variants, rename-rule previews, PDF merge ordering,
and crop single/multiple/summary/aspect-dial states in both themes. Setup captures welcome, options, progress,
done, update-failure and uninstall states in light/dark themes. Targeted screenshots were inspected after rendering.

Screenshots are generated under each test project's `bin/Release/net10.0/screenshots/` directory, outside the
runtime assets. Long file paths and user-supplied preset names can deliberately use ellipses; translated option
labels and their field captions wrap. Real Windows/macOS desktops, installation/UAC/uninstallation and assistive
technologies still need native verification. Headless rendering and simulated language providers cannot certify
that integration. The PR remains a draft for these checks and complete CI validation.

## Upstream coordination and next phase

Merged upstream fixes/features are integrated through `259c5ec`; no open feature branch is imported. Existing
PRs #242, #249, #275, #282, #283 and #291 were checked earlier and have since been merged or closed.

On 21 October a separate pt-BR contribution, [KnifeLemon/Filee#322](https://github.com/KnifeLemon/Filee/pull/322),
was found. Its catalog/resolver were read: it offers only pt-BR and routes Portuguese systems, including pt-PT,
to that variant. This fork's work predates it and stays in [Blackspirits/Filee#1](https://github.com/Blackspirits/Filee/pull/1).
Coordinate the Brazilian catalog before any upstream submission; retain separate pt-PT selection and detection.

French is absent from current upstream catalogs and no open French PR was found. Adding it is reasonable as a
separate localization contribution after the Portuguese/native checks. It is not part of this change.

Phase 2 remains pending: untrusted inputs and archive extraction, processes/downloads, temporary files,
cancellation/concurrency, conversion fidelity/metadata/color, memory/performance, dependencies, packaging,
keyboard/screen-reader accessibility and CI coverage. Findings need reproducible evidence before independent fixes.

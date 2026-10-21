# Portuguese localization review

Reviewed on 21 October 2026; integration now includes Filee 2.0.0 at upstream `799e39485f2aa78677f9ba51f692e862a7af76a6`.
The first implementation used `c63a261`, then integrated the installer at `85f7e56` on 20 October.
The 21 October review integrated `259c5ec`, then the newly merged Brazilian contribution at `941b01b`.
The final release merge changes only the version and changelog; it adds no English strings.
This report covers Phase 1. Security, conversion fidelity and performance still require the separate Phase 2 audit.

## Coverage and editorial review

Both Portuguese catalogs contain all **910 English keys**, in English key order: upstream's 889 entries and
18 descriptive engine names that previously came directly from C# metadata and three contextual donut labels.
These 21 added keys are translated in all eleven registered languages, including Arabic.

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

## Native desktop localization

The macOS bundle now declares both Portuguese variants and ships `ServicesMenu.strings` plus `InfoPlist.strings`
in `pt-PT.lproj` and `pt-BR.lproj`. Finder's service title matches `general.context_menu_label`; the English service
identifier remains unchanged so enabling/disabling the service and existing registrations still refer to the same
service. The two permission explanations retain distinct Portuguese terminology:

| English | pt-PT | pt-BR |
| --- | --- | --- |
| Convert with Filee | Converter com o Filee | Converter com o Filee |
| Filee reads selected Finder files when you use a conversion shortcut. | O Filee lê os ficheiros selecionados no Finder quando utilizas um atalho de conversão. | O Filee lê os arquivos selecionados no Finder quando você usa um atalho de conversão. |
| Filee detects your conversion shortcut and drag gesture to show the conversion menu. | O Filee deteta o atalho de conversão e o gesto de arrastar para mostrar o menu de conversão. | O Filee detecta o atalho de conversão e o gesto de arrastar para mostrar o menu de conversão. |

These are three native strings per variant, additional to the 910 JSON entries. macOS chooses native bundle texts
from its language preferences; these are not Avalonia resources switched by the app's JSON language setting.
The existing packaging loop copies every `.lproj`; it now validates each strings file with `plutil` on macOS before
copying. Cross-platform tests check declared folders, service identity, catalog consistency and variant-specific
permission wording. Actual Finder and permission-prompt display still require a native desktop check.

On Linux, `Filee Donut` is a technical window identifier used by the shipped KWin script. It must remain stable
across UI languages. Two contract cases check the window title against that script and across a pt-PT/pt-BR switch.
Visible toolbar strings remain localized.

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
| P2 | The macOS bundle has no Portuguese `.lproj` resources or bundle declarations, so native service/permission text cannot use the Portuguese catalogs. Inspect `installer/macos` and the packaged bundle. | Add both regional resources and declarations, keeping the service identifier stable; validate resources and packaging. Native display is still pending. |
| P3 | A separate test language list can omit newly registered catalogs; duplicate JSON keys are silently overwritten. | Use the shared registry and verify file parity, unique keys, non-empty values and placeholders. Retain upstream's new registry/Arabic coverage rather than adding a competing list. |

Portuguese tests compare placeholder multiplicity, named tokens, newline counts and Unicode NFC normalization,
and parse/format every numeric composite format. The global set-based placeholder check remains: other languages
can legitimately avoid repeating an argument.

## Validation

Recorded on Linux x64 with .NET SDK 10.0.402 and all **15 pinned Noto font files**, each SHA-256 verified:

| Check | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Release solution build (14 projects) | — | 0 errors | — |
| App localization, Portuguese UI and release/README contracts | 343 | 0 | 0 |
| Setup texts, UI states and language handoff | 66 | 0 | 1 |
| Core, full suite | 456 | 0 | 0 |
| CLI, full suite | 28 | 0 | 0 |
| Platform, full suite, including native resource contracts | 48 | 0 | 5 |

The focused/current checks total **941 unique passed tests and 6 platform-specific skips**. The App count is the
341-case completed selection plus two new KWin language-contract cases. The final 163-case follow-up covers changed captions,
localized resources and the release window; overlapping cases are not counted twice. Release builds complete with
no errors and retain existing obsolete-API/nullability warnings. All 15 pinned fonts are present and verified.

The broader Setup run on Linux completed **118 passed / 36 failed / 1 skipped**. Its 66 localization/UI/handoff
passes above are included in those 118, so across all completed project checks there are **993 unique passes,
36 failures and 6 skips**. This is not a green full-solution result. All 36 failure names exactly match a clean
checkout of upstream `799e394`, running its non-UI Setup tests (**53 passed / 36 failed**). Windows-style fixture
paths, Windows path normalization, PE imports and prior-install registry/path assumptions fail on Linux. No
production installer-path behavior or test exclusions were changed to conceal them. The earlier Windows CI
passed this suite; final Windows/native CI remains the appropriate check for these cases.

Baseline reproduction, on Linux, from an untouched `799e394` checkout:

```sh
dotnet test tests/Filee.Setup.Tests -m:1 -c Release --filter "FullyQualifiedName!~SetupTextsTests&FullyQualifiedName!~SetupWindowTests"
```

The native desktop cases still need real Windows/macOS sessions. The build and resource tests alone cannot
certify actual installation, UAC, Finder menus, permission dialogs or screen-reader behavior.

Reproduce the main checks from the repository root after fetching fonts with `build/fetch-fonts.ps1`:

```sh
dotnet build Filee.slnx -m:1 -c Release
dotnet test tests/Filee.App.Tests -m:1 -c Release --filter "FullyQualifiedName~Localization|FullyQualifiedName~Portuguese|FullyQualifiedName~InstallerTests|FullyQualifiedName~SupportersTests|FullyQualifiedName~ReleaseNotesTests|FullyQualifiedName~WhatsNewTests|FullyQualifiedName~DonutLabelPlacementTests|FullyQualifiedName~Home_page_search_finds_old_conversions_in_a_long_history|FullyQualifiedName~Watch_folder_options_rename_and_filter_with_a_preview|FullyQualifiedName~Preset_editor_shows_media_and_archive_options|FullyQualifiedName~One_picture_shows_the_frame_its_size_and_the_aspect_dial|FullyQualifiedName~Several_files_show_a_strip_with_how_each_went|FullyQualifiedName~Chooser_fits_long_paths_at_its_minimum_width|FullyQualifiedName~Live_photo_options_render_for_jpg_and_heic_and_save_only_there"
dotnet test tests/Filee.Setup.Tests -m:1 -c Release --filter "FullyQualifiedName~SetupTextsTests|FullyQualifiedName~SetupWindowTests|FullyQualifiedName~What_setup_hands_to_filee_is_what_filee_reads"
dotnet test tests/Filee.Core.Tests -m:1 -c Release
dotnet test tests/Filee.Cli.Tests -m:1 -c Release
dotnet test tests/Filee.Platform.Tests -m:1 -c Release
```

The full solution formatter cannot connect to Roslyn's build-host pipe here (`SocketException (13): Permission
denied`, host exit 137). Folder-based whitespace verification of the changed C# files and
`git diff --check` pass. On the previously published head `7a1813a`, the Windows CI job passed the full App suite (954 tests), Setup
(155), Core (454 passed / 2 skipped), CLI (27 / 1), Platform (48 / 3) and Engines (1,385 / 93), along with the
full formatter and installer sandbox. Eight of the nine CI jobs completed successfully. macOS Intel was cancelled
while running the App suite, so the overall run was not green. Those are results for the earlier head, not a final
CI certificate for the localized recordings added here. The Unix workflow now logs individual UI cases and captures
crash/hang diagnostics, matching Windows' five-minute UI hang timeout. New-head CI must be checked separately.

This is not a clean full-solution test result. Earlier broad engine checks on `c63a261` recorded 1,139 passes,
6 failures and 40 skips; five failures reproduced on an untouched checkout, while the remaining LibreOffice case
passed in isolation. That is historical evidence, not validation of the newer engine code in the current baseline. Earlier
unfiltered App runs also did not complete and encountered denied named-pipe sockets. The current completed App
run is the focused selection above. The CLI keeps its separate, English-only text provider.

## Localized demonstration media

The six single-language demonstrations in the 2.0.0 What's new window and installer now have recordings in both
Portuguese variants. They are rendered from real Filee controls, not edited screenshots. The language showcase
intentionally remains shared, since it demonstrates several languages. The original shared recordings are retained.

Each slide can declare `localizedMedia`, keyed by the exact canonical UI language, with a relative `media` path and
its own `poster` frame. Unknown languages use the shared recording; pt-PT never falls back to pt-BR. Invalid paths
are ignored by the same relative-path validation used for shared media. The app refreshes the picture when its UI
language changes, keeping the current slide. Setup uses its selected language too. Tests cover exact matching,
invalid entries, live switches, keyboard navigation, every localized resource and each reduced-motion poster.

The final visual pass found clipped image-action labels in the donut. Four new layout cases failed for the existing
Portuguese names. Contextual `.donut` keys now give the ring compact names while the editor retains the full names
and user-defined names retain priority:

| English action | pt-PT / pt-BR ring label | Full Portuguese name retained in the editor |
| --- | --- | --- |
| Half size | Tamanho 50 % | Metade do tamanho |
| Trace to SVG | Vetorizar | Vetorizar para SVG |
| Line art to SVG | Traços | Traços para SVG |

Six layout cases cover both variants and preserve custom names. Every media file stays below 400 KiB; each language
pack and the shared pack stay below 2 MiB. Together they must stay below 6 MiB. Regenerate from a built test project:

```sh
FILEE_WHATSNEW_LANGUAGE=pt-PT dotnet tests/Filee.App.Tests/bin/Release/net10.0/Filee.App.Tests.dll -method 'Filee.App.Tests.WhatsNewMedia.Generate_2_0_0' -explicit only
FILEE_WHATSNEW_LANGUAGE=pt-BR dotnet tests/Filee.App.Tests/bin/Release/net10.0/Filee.App.Tests.dll -method 'Filee.App.Tests.WhatsNewMedia.Generate_2_0_0' -explicit only
```

Rebuild after generation: the pictures are embedded in both App and Setup. Inspect the generated first-frame PNGs
and the What's new / installer screenshots, including the selected poster frames.

## Visual and native checks

Headless Skia screenshots cover nine app pages in both variants at the 820 px minimum, light/dark themes and
100%/200% render scaling, including the bottom/right portions of scrollable editors. Additional captures cover
image/PDF/video preset dialogs at 440/560 px, Live Photo options in both Portuguese variants, rename-rule previews, PDF merge ordering,
crop single/multiple/summary/aspect-dial states, and the release-notes window in both themes. Setup captures welcome, options, progress,
done, update-failure and uninstall states in light/dark themes. Targeted screenshots were inspected after rendering.

Screenshots are generated under each test project's `bin/Release/net10.0/screenshots/` directory, outside the
runtime assets. Long file paths and user-supplied preset names can deliberately use ellipses; translated option
labels and their field captions wrap. Real Windows/macOS desktops, installation/UAC/uninstallation and assistive
technologies still need native verification. Headless rendering and simulated language providers cannot certify
that integration. The PR remains a draft for these checks and complete CI validation.

### Remaining native verification

The following checks need actual desktops; the headless tests and CI sandbox are not a substitute:

- Windows display language pt-PT / pt-BR with `auto`, then an explicit opposite variant and a restart. Repeat with
  a different number/date region and verify that language changes do not change the regional format.
- Real Windows install, update, launch and uninstall in both variants, including UAC and the optional-engine page.
  The automated installer sandbox uses a stub app and a scratch registry; it does not cover that full interaction.
- macOS started from Finder with each Portuguese preferred language; check the explicit choice after restarting.
- Tab / Shift+Tab, Enter and Escape through the language picker, preset editor and installation. With NVDA on
  Windows and VoiceOver on macOS, confirm names, roles, focus and the two localized preset icon actions.

## Upstream coordination and next phase

Merged upstream fixes/features are integrated through `799e394` (2.0.0); no open feature branch is imported. Existing
PRs #242, #249, #275, #282, #283 and #291 were checked earlier and have since been merged or closed.

[KnifeLemon/Filee#322](https://github.com/KnifeLemon/Filee/pull/322) merged during this review. Its Brazilian
README, release announcements and added coverage are integrated. The fork keeps the independently reviewed
Brazilian catalog, with the same 889 upstream keys plus the 18 engine-name keys and three contextual donut labels. Tests added by #322 that assumed
pt-PT should use Brazilian Portuguese are updated to distinguish the variants. Existing Brazilian UI cases are
retained without duplicating the render-data entries already present here.

The contribution in [Blackspirits/Filee#1](https://github.com/Blackspirits/Filee/pull/1) now adds European Portuguese
and refines the Brazilian localization already present upstream. It targets this fork; any later upstream submission
should describe that final scope. The broad `pt` prefix fallback is replaced by the shared region policy, so an
unsupported code such as `ptx` does not accidentally select Portuguese.

French is absent from current upstream catalogs, but [KnifeLemon/Filee#359](https://github.com/KnifeLemon/Filee/pull/359)
now supplies it in an open contribution by another author. Do not duplicate that work. It is not imported here.

Phase 2 remains pending: untrusted inputs and archive extraction, processes/downloads, temporary files,
cancellation/concurrency, conversion fidelity/metadata/color, memory/performance, dependencies, packaging,
keyboard/screen-reader accessibility and CI coverage. Findings need reproducible evidence before independent fixes.

# Adding a language

1. Copy `src/Filee.App/Assets/i18n/en.json` to `<code>.json` (e.g. `ja.json`, `de.json`, `zh-TW.json`).
   The file is a flat list of `"key": "text"` pairs.
2. Translate the values. Keep `{0}`, `{1}` placeholders — a test checks they match English.
   Keep the text short where English is short (buttons, donut labels).
3. Register the language in `AppLanguages.All` (`src/Filee.App/Services/AppLanguages.cs`) with its native name,
   e.g. `("ja", "日本語")`; the order there is the order of the language pickers. Setup (`src/Filee.Setup`) offers it
   too: add the code to the `/LANG` list in `src/Filee.Setup/SetupOptions.cs` and a row to `SetupTextsTests`.
   If the OS culture should pick it automatically, extend `LocalizationService.Resolve`. Match the code itself and
   `<code>-*` only (`"fr"` and `"fr-CA"`, not North Frisian `"frr"`), and add rows for both to
   `LocalizationTests.Auto_follows_the_language_the_user_reads_the_os_in`.
4. `LocalizationTests.Languages` automatically checks every registered language for key and placeholder parity.
5. If the script needs another font, add a static Noto font to `build/fetch-fonts.ps1` (pinned with its SHA-256),
   to the font list in `SoftPalette.FontFamilies` and to the Noto line in `THIRD-PARTY-NOTICES.md` (variable fonts
   are not supported by Avalonia). Add the script's system UI fonts too, Windows' to `SoftPalette.FontFamilies` and
   `SoftPalette.SystemFontFamilies` (Setup draws with Windows' fonts only) and macOS' to `FontFamilies`, e.g.
   Leelawadee UI and Thonburi for Thai.
   A language written right to left (Arabic) mirrors every window by itself (`SoftPalette.ApplyFlowDirection`).
   Pictures and drawings that must not mirror (the donut, the crop canvas, animated images, logos) set
   `FlowDirection="LeftToRight"` on themselves; custom-drawn text is shaped in the UI culture's direction, as
   `DonutMenu` does.
6. Run `dotnet test` and look at the screenshots in `tests/Filee.App.Tests/bin/.../screenshots`
   (add your language to `RenderTests.Pages` to render a few pages).

Missing keys fall back to English at runtime, but the test suite requires every language to be complete.

Optional, but welcome in the same or a later pull request: a translated README. Add `README.<code>.md` with every
section `README.md` has, link it from the language line at the top of the other READMEs and add the language to
their list of UI languages. The maintainers add the language to the website (filee.sh).

## Adding a UI string

Add the key to **all** language files in the same pull request. In XAML use
`Text="{DynamicResource my.key}"`; in code use `ILocalizer["my.key"]` or `ILocalizer.Format("my.key", arg)`.

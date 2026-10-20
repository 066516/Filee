# Adding a language

1. Copy `src/Filee.App/Assets/i18n/en.json` to `<code>.json` (e.g. `ja.json`, `de.json`, `zh-TW.json`).
   The file is a flat list of `"key": "text"` pairs.
2. Translate the values. Keep `{0}`, `{1}` placeholders — a test checks they match English.
   Keep the text short where English is short (buttons, donut labels).
3. Register the language in `AppLanguages.All`
   (`src/Filee.App/Services/AppLanguages.cs`) with its native name, e.g. `("ja", "日本語")`.
   The app and installer share this registry. If the OS culture should pick it automatically, extend
   `LocalizationService.Resolve` and check regional codes in `SetupTexts.Resolve` too.
4. `LocalizationTests.Languages` automatically checks every registered language for key and placeholder parity.
   Tests also require every translation file to be registered and offered by the installer.
5. If the script needs another font, add a static Noto font to `build/fetch-fonts.ps1`
   and to the font list in `SoftPalette` (variable fonts are not supported by Avalonia).
   The installer uses `SoftPalette.SystemFontFamilies` instead of embedded fonts; verify its script coverage too.
   Keep the app's left-to-right layout in every language. Arabic text still uses bidirectional shaping;
   custom-drawn controls must shape text in the UI culture's direction, as `DonutMenu` does.
6. Run `dotnet test` and look at the screenshots in `tests/Filee.App.Tests/bin/.../screenshots`
   (add your language to `RenderTests.Pages` to render a few pages). Review the installer screenshots in
   `tests/Filee.Setup.Tests/bin/.../screenshots` as well; all registered languages render automatically.

Missing keys fall back to English at runtime, but the test suite requires every language to be complete.

Optional, but welcome in the same or a later pull request: a translated README. Add `README.<code>.md` and link
it from the language line at the top of the other READMEs.

## Adding a UI string

Add the key to **all** language files in the same pull request. In XAML use
`Text="{DynamicResource my.key}"`; in code use `ILocalizer["my.key"]` or `ILocalizer.Format("my.key", arg)`.

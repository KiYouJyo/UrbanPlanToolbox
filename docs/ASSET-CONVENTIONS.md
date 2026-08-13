# Theme-aware asset naming

Theme names describe the target environment where an asset is used, never the asset's own foreground color.

| Target environment | Required foreground for a transparent UrbanPlanToolbox logo |
| --- | --- |
| App Dark Theme | White |
| App Light Theme | Black |
| Windows Shell Dark Theme | White |
| Windows Shell Light Theme | Black |

Use `ForDarkTheme` and `ForLightTheme` for app-window assets. Use `ForDarkShellTheme` and `ForLightShellTheme` in code, scripts, comments, and tests for Taskbar/Start assets. The physical color may be named explicitly only when needed: `White...Source` and `Black...Source` describe image color, while `For...Theme` describes usage.

Do not infer that `Dark` means a black image or that `Light` means a white image. A white logo is selected for a dark target environment; a black logo is selected for a light target environment.

Dark target environment → white foreground. Light target environment → black foreground.

## App theme and Shell theme

App Theme is the UrbanPlanToolbox Light/Dark/System setting. It controls Mica, the title-bar logo, caption-button theme, and startup overlay. Windows Shell Theme independently controls Taskbar and Start package-resource selection. Mixed themes are expected: with Windows Light + App Dark, the title bar is white while Taskbar and Start use black.

## MRT resources

Windows MRT qualifier names remain platform-defined. A `theme-light` candidate is for a light Shell surface and therefore carries the black foreground logo. The default Shell candidate is the dark-surface white foreground logo. `altform-unplated` keeps the transparent, unplated visual.

## v1.6.7 reference implementation

`WindowIconTheme` maps `IconForDarkTheme` to the white runtime ICO and `IconForLightTheme` to the black runtime ICO. `StartupSplashPresentation` follows the same semantic mapping without changing the accepted splash timing, size, DPI, Mica, or fade behavior. Existing physical files such as `Icon-Large-Dark-1024.png` are retained for package/PRI stability; their historical filenames are not a color contract.

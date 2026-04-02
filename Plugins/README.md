# Fluxor Plugins

This folder is the source workspace for DLL-only Fluxor plugins.

For installed builds, Fluxor opens the user workspace here:
- `%USERPROFILE%\Documents\Fluxor\Plugins`

Included sample projects:
- `Fluxor.PluginCursorFlameContour`
  - cursor-alpha based flame contour and molten echo
- `Fluxor.PluginVelvetVoid`
  - dense black void ribbon with a soft cosmic core
- `Fluxor.PluginFireflySwarm`
  - sparse glowing fireflies with curved micro-trails

Recommended flow:
1. Duplicate one of the sample folders or create a new folder here.
2. Build against `CursorFX.Core`.
   - inside the repository the sample projects use `..\..\CursorFX.Core\CursorFX.Core.csproj`
   - in installed setups they fall back to `..\Fluxor.PluginSdk\CursorFX.Core.dll`
3. Implement `ICursorEffectPlugin`.
4. Expose metadata and settings directly from code via:
   - `DisplayName`
   - `PluginId`
   - `Description`
   - `IconGlyph`
   - `AccentColor`
   - `Kind`
   - `Trigger`
   - `GetParameters()`
   - mark expert-only controls with `IsAdvanced = true` in your parameter definitions
5. Build the project.
6. In Fluxor click `Import Plugin`.
7. Choose the built DLL from the plugin folder.

Preferred implementation style:
- use `PluginRenderContext` in `Update(...)`, `Render(...)`, `OnMouseMove(...)`, and `OnMouseClick(...)`
- read `CursorPosition` for cursor-locked visuals
- read `CursorSnapshot` when you need the real cursor alpha/shape
- read `BackdropSample` when you want distortion, refraction, or other screen-reactive effects

Quick start example:
1. Copy a sample folder such as `Fluxor.PluginHeatContour`.
2. Rename the folder, `.csproj`, namespace, class, `DisplayName`, and `PluginId`.
3. Keep `GetParameters()` small at first:
   - one `Number`
   - one `Color`
   - one `Toggle`
4. Build:
   - `dotnet build .\Fluxor.PluginYourEffect\Fluxor.PluginYourEffect.csproj -m:1 -v minimal`
5. Import the produced DLL into Fluxor and tune it live.

Notes:
- Keep one plugin project per folder.
- Fluxor imports DLL metadata directly, so JSON manifests are no longer required.
- Avoid committing `bin` and `obj` folders; they are regenerated on build.

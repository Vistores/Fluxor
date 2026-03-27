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

Notes:
- Keep one plugin project per folder.
- Fluxor imports DLL metadata directly, so JSON manifests are no longer required.
- Avoid committing `bin` and `obj` folders; they are regenerated on build.

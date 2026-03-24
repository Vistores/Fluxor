# Fluxor Plugins

This folder is the default workspace for DLL-only Fluxor plugins.

Included sample projects:
- `Fluxor.PluginCursorFlameContour`
  - cursor-alpha based flame contour and molten echo
- `Fluxor.PluginVelvetVoid`
  - dense black void ribbon with a soft cosmic core
- `Fluxor.PluginFireflySwarm`
  - sparse glowing fireflies with curved micro-trails

Recommended flow:
1. Duplicate one of the sample folders or create a new folder here.
2. Reference `..\..\CursorFX.Core\CursorFX.Core.csproj`.
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
5. Build the project.
6. In Fluxor click `Import Plugin`.
7. Choose the built DLL from the plugin folder.

Notes:
- Keep one plugin project per folder.
- Fluxor imports DLL metadata directly, so JSON manifests are no longer required.
- Avoid committing `bin` and `obj` folders; they are regenerated on build.

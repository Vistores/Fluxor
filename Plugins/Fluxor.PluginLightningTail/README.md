# Fluxor.PluginLightningTail

Standalone Fluxor plugin that renders a cursor-locked lightning tail with sharp forks and a thunder-strike tap impact.

The plugin is intentionally stored outside the main Fluxor project folder:

```text
C:\Users\Gyrocopter_UA\Desktop\FluxorPlugins\Fluxor.PluginLightningTail
```

## Effect

- 1:1 cursor-locked lightning head with no smoothing delay.
- Dense high-speed sampling to avoid gaps during fast cursor motion.
- Jagged bolt geometry with configurable fork amount and sharpness.
- Tap impact that creates a short lightning strike burst.
- Editable colors for core, bolt, glow, and impact.

## Build

```powershell
Set-Location C:\Users\Gyrocopter_UA\Desktop\FluxorPlugins\Fluxor.PluginLightningTail
dotnet build .\Fluxor.PluginLightningTail.csproj -m:1 -v minimal
```

## Import DLL

Import this DLL in Fluxor:

```text
C:\Users\Gyrocopter_UA\Desktop\FluxorPlugins\Fluxor.PluginLightningTail\bin\Debug\net9.0-windows\Fluxor.PluginLightningTail.dll
```

## Parameters

- `Core Color`
- `Bolt Color`
- `Edge Glow`
- `Impact Color`
- `Opacity`
- `Tail Lifetime`
- `Point Spacing`
- `Bolt Thickness`
- `Glow Size`
- `Sharpness Jitter`
- `Fork Amount`
- `Fork Length`
- `Flicker`
- `Impact Radius`
- `Impact Lifetime`
- `Impact Bolts`

# Fluxor.PluginSakuraInk

Standalone Fluxor plugin that combines drifting sakura petals with ink-blot cursor traces and a stronger tap burst.

The plugin lives outside the main Fluxor project:

```text
C:\Users\Gyrocopter_UA\Desktop\FluxorPlugins\Fluxor.PluginSakuraInk
```

## Effect

- Passive sakura petals drift calmly away from the cursor.
- Ink blots remain behind as a soft expressive trail.
- Clicking creates a stronger petal burst and larger ink splash.
- Petal colors, ink colors, lifetime, density, burst power, and motion can be tuned in Fluxor.

## Build

```powershell
Set-Location C:\Users\Gyrocopter_UA\Desktop\FluxorPlugins\Fluxor.PluginSakuraInk
dotnet build .\Fluxor.PluginSakuraInk.csproj -m:1 -v minimal
```

## Import DLL

Import this DLL in Fluxor:

```text
C:\Users\Gyrocopter_UA\Desktop\FluxorPlugins\Fluxor.PluginSakuraInk\bin\Debug\net9.0-windows\Fluxor.PluginSakuraInk.dll
```

## Main Parameters

- `Petal Color`
- `Petal Highlight`
- `Ink Color`
- `Ink Edge Color`
- `Passive Petal Rate`
- `Petal Size`
- `Petal Lifetime`
- `Petal Speed`
- `Ink Trail Rate`
- `Ink Blot Size`
- `Ink Lifetime`
- `Tap Petals`
- `Tap Ink Blots`
- `Tap Burst Power`
- `Petal Swirl`
- `Falling Drift`
- `Ink Bleed`

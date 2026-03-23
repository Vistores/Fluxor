namespace CursorFX.Core.Models;

public sealed class EffectPreset
{
    public required string Name { get; init; }

    public required AppSettings Settings { get; init; }
}

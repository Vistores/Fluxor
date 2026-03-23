using CursorFX.Core.Models;

namespace CursorFX.Core.Interfaces;

public interface ISettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}

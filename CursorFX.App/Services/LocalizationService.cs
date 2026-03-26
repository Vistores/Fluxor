using System.Globalization;
using CursorFX.Core.Models;

namespace CursorFX.App.Services;

public sealed class LocalizationService
{
    private static readonly IReadOnlyList<LocalizationOption> SupportedLanguages =
    [
        new("en", "English"),
        new("uk", "Українська"),
        new("ru", "Русский")
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Strings =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["settings.windowTitle"] = "Fluxor · Settings",
                ["settings.heading"] = "Fluxor Settings",
                ["settings.intro"] = "Control how Fluxor starts, behaves in the background, and which language should be used for the interface.",
                ["settings.startup"] = "Startup",
                ["settings.launchOnStartup"] = "Launch Fluxor when Windows starts",
                ["settings.backgroundMode"] = "Background Mode",
                ["settings.runInBackground"] = "Keep Fluxor running in the tray when the main window is closed or minimized",
                ["settings.renderGuard"] = "Render Guard",
                ["settings.pauseWhenCursorHidden"] = "Pause effects when a fullscreen app hides the cursor",
                ["settings.language"] = "Language",
                ["settings.useSystemLanguage"] = "Use system language automatically",
                ["settings.languageLabel"] = "Application language",
                ["settings.languageHint"] = "Language changes apply to newly opened windows first. A full UI rollout will continue in upcoming updates.",
                ["settings.cancel"] = "Cancel",
                ["settings.apply"] = "Apply",
                ["settings.updated"] = "Application settings updated."
            },
            ["uk"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["settings.windowTitle"] = "Fluxor · Налаштування",
                ["settings.heading"] = "Налаштування Fluxor",
                ["settings.intro"] = "Керуйте автозапуском Fluxor, роботою у фоновому режимі та мовою інтерфейсу.",
                ["settings.startup"] = "Автозапуск",
                ["settings.launchOnStartup"] = "Запускати Fluxor разом із Windows",
                ["settings.backgroundMode"] = "Фоновий режим",
                ["settings.runInBackground"] = "Залишати Fluxor у треї після закриття або згортання головного вікна",
                ["settings.renderGuard"] = "Захист рендера",
                ["settings.pauseWhenCursorHidden"] = "Призупиняти ефекти, коли fullscreen-застосунок ховає курсор",
                ["settings.language"] = "Мова",
                ["settings.useSystemLanguage"] = "Автоматично використовувати системну мову",
                ["settings.languageLabel"] = "Мова застосунку",
                ["settings.languageHint"] = "Зміна мови спершу застосовується до нових вікон. Повна локалізація решти UI буде розширюватися в наступних оновленнях.",
                ["settings.cancel"] = "Скасувати",
                ["settings.apply"] = "Застосувати",
                ["settings.updated"] = "Параметри застосунку оновлено."
            },
            ["ru"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["settings.windowTitle"] = "Fluxor · Настройки",
                ["settings.heading"] = "Настройки Fluxor",
                ["settings.intro"] = "Управляйте автозапуском Fluxor, фоновым режимом и языком интерфейса.",
                ["settings.startup"] = "Автозапуск",
                ["settings.launchOnStartup"] = "Запускать Fluxor вместе с Windows",
                ["settings.backgroundMode"] = "Фоновый режим",
                ["settings.runInBackground"] = "Оставлять Fluxor в трее после закрытия или сворачивания главного окна",
                ["settings.renderGuard"] = "Защита рендера",
                ["settings.pauseWhenCursorHidden"] = "Приостанавливать эффекты, когда fullscreen-приложение скрывает курсор",
                ["settings.language"] = "Язык",
                ["settings.useSystemLanguage"] = "Автоматически использовать системный язык",
                ["settings.languageLabel"] = "Язык приложения",
                ["settings.languageHint"] = "Смена языка сначала применяется к новым окнам. Полная локализация остального UI будет расширяться в следующих обновлениях.",
                ["settings.cancel"] = "Отмена",
                ["settings.apply"] = "Применить",
                ["settings.updated"] = "Параметры приложения обновлены."
            }
        };

    public IReadOnlyList<LocalizationOption> AvailableLanguages => SupportedLanguages;

    public string CurrentLanguageCode { get; private set; } = "en";

    public void Apply(LocalizationSettings settings)
    {
        CurrentLanguageCode = ResolveLanguageCode(settings);
        var culture = new CultureInfo(CurrentLanguageCode);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }

    public string ResolveLanguageCode(LocalizationSettings settings)
    {
        if (settings.UseSystemLanguage)
        {
            return ResolveSystemLanguageCode();
        }

        return NormalizeLanguageCode(settings.LanguageCode);
    }

    public string ResolveSystemLanguageCode()
    {
        return NormalizeLanguageCode(CultureInfo.InstalledUICulture.TwoLetterISOLanguageName);
    }

    public string NormalizeLanguageCode(string? languageCode)
    {
        var normalized = string.IsNullOrWhiteSpace(languageCode) ? "en" : languageCode.Trim().ToLowerInvariant();
        return SupportedLanguages.Any(option => string.Equals(option.Code, normalized, StringComparison.OrdinalIgnoreCase))
            ? normalized
            : "en";
    }

    public string Get(string key)
    {
        if (Strings.TryGetValue(CurrentLanguageCode, out var languageStrings) &&
            languageStrings.TryGetValue(key, out var localized))
        {
            return localized;
        }

        return Strings["en"].TryGetValue(key, out var fallback) ? fallback : key;
    }
}

public sealed record LocalizationOption(string Code, string DisplayName);

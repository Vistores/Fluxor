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
                ["settings.updated"] = "Application settings updated.",
                ["import.windowTitle"] = "Fluxor | Import Plugin",
                ["import.heading"] = "Import Plugin",
                ["import.intro"] = "Choose a plugin DLL. Fluxor will read plugin metadata and parameter schema directly from the assembly, generate the profile automatically, and copy companion runtime files when they exist.",
                ["import.assemblyTitle"] = "DLL Assembly File",
                ["import.assemblyHint"] = "The DLL must contain a public ICursorEffectPlugin implementation and expose settings via GetParameters().",
                ["import.browseDll"] = "Browse DLL File",
                ["import.openPluginsFolder"] = "Open Plugins Folder",
                ["import.pluginTypeTitle"] = "Plugin Type",
                ["import.pluginTypeHint"] = "If the DLL contains multiple plugins, choose which one should be imported.",
                ["import.previewTitle"] = "Import Preview",
                ["import.previewHint"] = "Fluxor will generate the plugin profile directly from the selected assembly metadata.",
                ["import.preview.displayName"] = "Display name",
                ["import.preview.pluginId"] = "Plugin ID",
                ["import.preview.entryType"] = "Entry type",
                ["import.iconTitle"] = "Plugin Icon",
                ["import.iconHint"] = "Optional image for the plugin card. Fluxor will copy it to the plugin catalog and render it as a square icon.",
                ["import.chooseIcon"] = "Choose Icon",
                ["import.clearIcon"] = "Clear Icon",
                ["import.iconPlaceholder"] = "Icon",
                ["import.iconNone"] = "No icon selected.",
                ["import.cancel"] = "Cancel",
                ["import.confirm"] = "Import Plugin",
                ["import.validation.title"] = "Validation",
                ["import.validation.chooseDll"] = "Choose a DLL file.",
                ["import.validation.choosePluginType"] = "Choose a plugin type from the DLL.",
                ["guide.windowTitle"] = "Fluxor · Plugin Authoring Guide",
                ["guide.heading"] = "Plugin Authoring Guide",
                ["guide.intro"] = "How to build DLL-only Fluxor plugins, expose parameter schema from code, and avoid runtime or import failures.",
                ["guide.copy"] = "Copy Guide",
                ["guide.close"] = "Close",
                ["guide.missing"] = "Plugin authoring guide was not found.",
                ["main.importedStatus"] = "Plugin {0} imported from DLL.",
                ["main.heroSubtitle"] = "A cleaner control center for cursor effects, built-in styles, and DLL plugins.",
                ["main.applicationSettings"] = "Application Settings",
                ["main.importPlugin"] = "Import Plugin",
                ["main.currentProfile"] = "Current Profile",
                ["main.saveSettings"] = "Save Settings",
                ["main.resetProfile"] = "Reset Profile",
                ["main.chooseIcon"] = "Choose Icon",
                ["main.clearIcon"] = "Clear Icon",
                ["main.openPluginFolder"] = "Open Plugin Folder",
                ["main.deletePlugin"] = "Delete Plugin",
                ["main.status"] = "Status",
                ["main.generalControls"] = "General Controls",
                ["main.masterOpacity"] = "Master opacity",
                ["main.fpsCap"] = "FPS cap",
                ["main.cursorAttach"] = "Cursor attach",
                ["main.cursorAttachHint"] = "Use higher attach values when you want built-in effects to stay visually glued to the cursor during fast motion.",
                ["main.pluginAuthoring"] = "Plugin Authoring",
                ["main.pluginAuthoringHint"] = "Fluxor supports DLL-only plugins. Open the guide to see how to create a plugin, expose parameters from code, build the DLL, and import it into the app.",
                ["main.openAuthoringGuide"] = "Open Authoring Guide",
                ["main.profileControls"] = "Profile Controls",
                ["main.profileControlsHint"] = "Core controls are shown first. Advanced controls stay available but out of the way.",
                ["main.basic"] = "Basic",
                ["main.basicHint"] = "The most useful controls for shaping the current profile quickly.",
                ["main.advanced"] = "Advanced",
                ["main.advancedHint"] = "Less common controls for motion tuning, noise, spawn rules, and profile-specific behavior.",
                ["main.pluginDiagnostics"] = "Plugin Diagnostics",
                ["main.diag.status"] = "Status",
                ["main.diag.assembly"] = "Assembly",
                ["main.diag.assemblyPath"] = "Assembly path",
                ["main.diag.entryType"] = "Entry type",
                ["main.diag.loadedAt"] = "Loaded at",
                ["main.diag.lastError"] = "Last error",
                ["main.profiles"] = "Profiles",
                ["main.profilesHint"] = "Built-in styles stay separate from imported DLL plugins so the library is easier to scan.",
                ["main.builtInProfiles"] = "Built-in Profiles",
                ["main.importedPlugins"] = "Imported Plugins",
                ["main.importShort"] = "Import",
                ["main.iconPlaceholder"] = "Icon",
                ["main.autosaveReady"] = "Settings are synced automatically.",
                ["main.noImportedPlugins"] = "No imported plugins yet.",
                ["main.importedPluginsSummary"] = "Imported plugins sorted by newest first ({0}).",
                ["main.builtInProfilesSummary"] = "{0} built-in profiles."
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
                ["settings.updated"] = "Параметри застосунку оновлено.",
                ["import.windowTitle"] = "Fluxor | Імпорт плагіна",
                ["import.heading"] = "Імпорт плагіна",
                ["import.intro"] = "Оберіть DLL плагіна. Fluxor прочитає metadata та схему параметрів прямо зі збірки, автоматично згенерує профіль і скопіює супутні runtime-файли, якщо вони існують.",
                ["import.assemblyTitle"] = "DLL-файл збірки",
                ["import.assemblyHint"] = "DLL має містити публічну реалізацію ICursorEffectPlugin і описувати параметри через GetParameters().",
                ["import.browseDll"] = "Обрати DLL",
                ["import.openPluginsFolder"] = "Відкрити папку Plugins",
                ["import.pluginTypeTitle"] = "Тип плагіна",
                ["import.pluginTypeHint"] = "Якщо DLL містить кілька плагінів, оберіть, який саме потрібно імпортувати.",
                ["import.previewTitle"] = "Попередній перегляд імпорту",
                ["import.previewHint"] = "Fluxor згенерує профіль плагіна прямо з metadata вибраної збірки.",
                ["import.preview.displayName"] = "Назва",
                ["import.preview.pluginId"] = "Plugin ID",
                ["import.preview.entryType"] = "Entry type",
                ["import.iconTitle"] = "Іконка плагіна",
                ["import.iconHint"] = "Необов’язкове зображення для картки плагіна. Fluxor скопіює його в каталог плагінів і покаже як квадратну іконку.",
                ["import.chooseIcon"] = "Обрати іконку",
                ["import.clearIcon"] = "Очистити іконку",
                ["import.iconPlaceholder"] = "Іконка",
                ["import.iconNone"] = "Іконку не вибрано.",
                ["import.cancel"] = "Скасувати",
                ["import.confirm"] = "Імпортувати плагін",
                ["import.validation.title"] = "Перевірка",
                ["import.validation.chooseDll"] = "Оберіть DLL-файл.",
                ["import.validation.choosePluginType"] = "Оберіть тип плагіна з DLL.",
                ["guide.windowTitle"] = "Fluxor · Гайд зі створення плагінів",
                ["guide.heading"] = "Гайд зі створення плагінів",
                ["guide.intro"] = "Як створювати DLL-only плагіни для Fluxor, описувати схему параметрів у коді та уникати помилок імпорту чи runtime.",
                ["guide.copy"] = "Скопіювати гайд",
                ["guide.close"] = "Закрити",
                ["guide.missing"] = "Гайд зі створення плагінів не знайдено.",
                ["main.importedStatus"] = "Плагін {0} імпортовано з DLL.",
                ["main.heroSubtitle"] = "Чистіший центр керування ефектами курсора, вбудованими стилями та DLL-плагінами.",
                ["main.applicationSettings"] = "Налаштування застосунку",
                ["main.importPlugin"] = "Імпортувати плагін",
                ["main.currentProfile"] = "Поточний профіль",
                ["main.saveSettings"] = "Зберегти параметри",
                ["main.resetProfile"] = "Скинути профіль",
                ["main.chooseIcon"] = "Обрати іконку",
                ["main.clearIcon"] = "Очистити іконку",
                ["main.openPluginFolder"] = "Відкрити папку плагінів",
                ["main.deletePlugin"] = "Видалити плагін",
                ["main.status"] = "Стан",
                ["main.generalControls"] = "Загальні параметри",
                ["main.masterOpacity"] = "Загальна прозорість",
                ["main.fpsCap"] = "Ліміт FPS",
                ["main.cursorAttach"] = "Прилипання до курсора",
                ["main.cursorAttachHint"] = "Збільшуйте параметр, якщо хочете, щоб вбудовані ефекти візуально майже не відставали від курсора навіть під час різких рухів.",
                ["main.pluginAuthoring"] = "Створення плагінів",
                ["main.pluginAuthoringHint"] = "Fluxor підтримує DLL-only плагіни. Відкрий гайд, щоб побачити, як створити плагін, описати параметри в коді, зібрати DLL і імпортувати її в застосунок.",
                ["main.openAuthoringGuide"] = "Відкрити гайд",
                ["main.profileControls"] = "Параметри профілю",
                ["main.profileControlsHint"] = "Основні параметри показані першими. Розширені залишаються доступними, але не заважають.",
                ["main.basic"] = "Основні",
                ["main.basicHint"] = "Найкорисніші параметри для швидкого налаштування поточного профілю.",
                ["main.advanced"] = "Розширені",
                ["main.advancedHint"] = "Рідше вживані параметри для motion tuning, noise, spawn rules та профільної логіки.",
                ["main.pluginDiagnostics"] = "Діагностика плагіна",
                ["main.diag.status"] = "Стан",
                ["main.diag.assembly"] = "Збірка",
                ["main.diag.assemblyPath"] = "Шлях до збірки",
                ["main.diag.entryType"] = "Entry type",
                ["main.diag.loadedAt"] = "Завантажено",
                ["main.diag.lastError"] = "Остання помилка",
                ["main.profiles"] = "Профілі",
                ["main.profilesHint"] = "Вбудовані стилі відокремлені від імпортованих DLL-плагінів, щоб бібліотеку було легше сканувати.",
                ["main.builtInProfiles"] = "Вбудовані профілі",
                ["main.importedPlugins"] = "Імпортовані плагіни",
                ["main.importShort"] = "Імпорт",
                ["main.iconPlaceholder"] = "Іконка",
                ["main.autosaveReady"] = "Параметри синхронізуються автоматично.",
                ["main.noImportedPlugins"] = "Імпортованих плагінів ще немає.",
                ["main.importedPluginsSummary"] = "Імпортовані плагіни відсортовано від найновіших ({0}).",
                ["main.builtInProfilesSummary"] = "Вбудованих профілів: {0}."
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
                ["settings.updated"] = "Параметры приложения обновлены.",
                ["import.windowTitle"] = "Fluxor | Импорт плагина",
                ["import.heading"] = "Импорт плагина",
                ["import.intro"] = "Выберите DLL плагина. Fluxor прочитает metadata и схему параметров прямо из сборки, автоматически создаст профиль и скопирует сопутствующие runtime-файлы, если они существуют.",
                ["import.assemblyTitle"] = "DLL-файл сборки",
                ["import.assemblyHint"] = "DLL должна содержать публичную реализацию ICursorEffectPlugin и описывать параметры через GetParameters().",
                ["import.browseDll"] = "Выбрать DLL",
                ["import.openPluginsFolder"] = "Открыть папку Plugins",
                ["import.pluginTypeTitle"] = "Тип плагина",
                ["import.pluginTypeHint"] = "Если DLL содержит несколько плагинов, выберите, какой именно нужно импортировать.",
                ["import.previewTitle"] = "Предпросмотр импорта",
                ["import.previewHint"] = "Fluxor сгенерирует профиль плагина прямо из metadata выбранной сборки.",
                ["import.preview.displayName"] = "Название",
                ["import.preview.pluginId"] = "Plugin ID",
                ["import.preview.entryType"] = "Entry type",
                ["import.iconTitle"] = "Иконка плагина",
                ["import.iconHint"] = "Необязательное изображение для карточки плагина. Fluxor скопирует его в каталог плагинов и покажет как квадратную иконку.",
                ["import.chooseIcon"] = "Выбрать иконку",
                ["import.clearIcon"] = "Очистить иконку",
                ["import.iconPlaceholder"] = "Иконка",
                ["import.iconNone"] = "Иконка не выбрана.",
                ["import.cancel"] = "Отмена",
                ["import.confirm"] = "Импортировать плагин",
                ["import.validation.title"] = "Проверка",
                ["import.validation.chooseDll"] = "Выберите DLL-файл.",
                ["import.validation.choosePluginType"] = "Выберите тип плагина из DLL.",
                ["guide.windowTitle"] = "Fluxor · Гайд по созданию плагинов",
                ["guide.heading"] = "Гайд по созданию плагинов",
                ["guide.intro"] = "Как создавать DLL-only плагины для Fluxor, описывать схему параметров в коде и избегать ошибок импорта или runtime.",
                ["guide.copy"] = "Скопировать гайд",
                ["guide.close"] = "Закрыть",
                ["guide.missing"] = "Гайд по созданию плагинов не найден.",
                ["main.importedStatus"] = "Плагин {0} импортирован из DLL.",
                ["main.heroSubtitle"] = "Более чистый центр управления эффектами курсора, встроенными стилями и DLL-плагинами.",
                ["main.applicationSettings"] = "Настройки приложения",
                ["main.importPlugin"] = "Импортировать плагин",
                ["main.currentProfile"] = "Текущий профиль",
                ["main.saveSettings"] = "Сохранить параметры",
                ["main.resetProfile"] = "Сбросить профиль",
                ["main.chooseIcon"] = "Выбрать иконку",
                ["main.clearIcon"] = "Очистить иконку",
                ["main.openPluginFolder"] = "Открыть папку плагинов",
                ["main.deletePlugin"] = "Удалить плагин",
                ["main.status"] = "Статус",
                ["main.generalControls"] = "Общие параметры",
                ["main.masterOpacity"] = "Общая прозрачность",
                ["main.fpsCap"] = "Лимит FPS",
                ["main.cursorAttach"] = "Прилипание к курсору",
                ["main.cursorAttachHint"] = "Увеличивайте параметр, если хотите, чтобы встроенные эффекты визуально почти не отставали от курсора даже при резких движениях.",
                ["main.pluginAuthoring"] = "Создание плагинов",
                ["main.pluginAuthoringHint"] = "Fluxor поддерживает DLL-only плагины. Откройте гайд, чтобы увидеть, как создать плагин, описать параметры в коде, собрать DLL и импортировать её в приложение.",
                ["main.openAuthoringGuide"] = "Открыть гайд",
                ["main.profileControls"] = "Параметры профиля",
                ["main.profileControlsHint"] = "Основные параметры показаны первыми. Расширенные остаются доступными, но не мешают.",
                ["main.basic"] = "Основные",
                ["main.basicHint"] = "Самые полезные параметры для быстрого изменения текущего профиля.",
                ["main.advanced"] = "Расширенные",
                ["main.advancedHint"] = "Более редкие параметры для motion tuning, noise, spawn rules и логики конкретного профиля.",
                ["main.pluginDiagnostics"] = "Диагностика плагина",
                ["main.diag.status"] = "Статус",
                ["main.diag.assembly"] = "Сборка",
                ["main.diag.assemblyPath"] = "Путь к сборке",
                ["main.diag.entryType"] = "Entry type",
                ["main.diag.loadedAt"] = "Загружено",
                ["main.diag.lastError"] = "Последняя ошибка",
                ["main.profiles"] = "Профили",
                ["main.profilesHint"] = "Встроенные стили отделены от импортированных DLL-плагинов, чтобы библиотеку было легче просматривать.",
                ["main.builtInProfiles"] = "Встроенные профили",
                ["main.importedPlugins"] = "Импортированные плагины",
                ["main.importShort"] = "Импорт",
                ["main.iconPlaceholder"] = "Иконка",
                ["main.autosaveReady"] = "Параметры синхронизируются автоматически.",
                ["main.noImportedPlugins"] = "Импортированных плагинов пока нет.",
                ["main.importedPluginsSummary"] = "Импортированные плагины отсортированы от самых новых ({0}).",
                ["main.builtInProfilesSummary"] = "Встроенных профилей: {0}."
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

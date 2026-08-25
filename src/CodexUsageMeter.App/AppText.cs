using System.Globalization;

namespace CodexUsageMeter.App;

public static class AppText
{
    public const string English = "en-US";
    public const string Spanish = "es-ES";
    private static readonly IReadOnlyDictionary<string, string> En = new Dictionary<string, string>
    {
        ["Searching"] = "Codex Usage Meter: looking for data...", ["StartWindows"] = "Start with Windows",
        ["ShowPinned"] = "Show pinned widget", ["Available"] = "Available: {0}%  ·  Used: {1}%",
        ["ResetAt"] = "Resets: {0}", ["NoReset"] = "Reset: no data", ["NoUsage"] = "No usage data",
        ["RunTask"] = "Open Codex and run at least one task", ["Refresh"] = "Refresh now",
        ["KeepTray"] = "Always show in system tray...",
        ["Notifications"] = "Notifications", ["NotifyChange"] = "When the percentage changes",
        ["Notify50"] = "At 50% used", ["Notify75"] = "At 75% used", ["Notify90"] = "At 90% used",
        ["NotifyReset"] = "When the limit resets", ["Exit"] = "Exit", ["Widget"] = "Widget",
        ["Disabled"] = "Disabled", ["Normal"] = "Normal", ["Compact"] = "Compact", ["UsageBar"] = "Usage bar",
        ["UsageBarEnabled"] = "Enabled", ["Thickness"] = "Thickness",
        ["Display"] = "Display", ["Automatic"] = "Automatic", ["AllDisplays"] = "All displays", ["Primary"] = "Primary",
        ["Language"] = "Language", ["UsageChanged"] = "Codex usage changed",
        ["Threshold"] = "Codex reached {0}% usage", ["LimitReset"] = "The Codex limit has reset",
        ["BalloonUsage"] = "Codex usage", ["BalloonUnavailable"] = "Codex usage unavailable",
        ["BalloonNoData"] = "Run a Codex task and refresh.", ["AvailableUsed"] = "{0}% available ({1}% used).",
        ["Pin"] = "Pin widget", ["Unpin"] = "Unpin widget", ["NoData"] = "No data",
        ["WaitingTask"] = "Run a task in Codex", ["AutoUpdate"] = "The card will update automatically",
        ["ResetsUnknown"] = "Available resets: no data", ["AvailableText"] = "{0}% available",
        ["UsedText"] = "{0}% used", ["Updated"] = "Updated {0}", ["Resets"] = "Available resets: {0}",
        ["ResetPending"] = "Usage reset pending", ["ResetUnderDay"] = "Usage resets in less than 1 day",
        ["ResetOneDay"] = "Usage resets in 1 day", ["ResetDays"] = "Usage resets in {0} days",
        ["WindowResets"] = "{0}: {1}", ["ResetNow"] = "now",
        ["OneReset"] = "1 reset available", ["ManyResets"] = "{0} resets available",
        ["Credits"] = "Credits: {0}", ["NoCredits"] = "Credits: no data", ["Stale"] = "Stale",
        ["JustNow"] = "just now", ["MinutesAgo"] = "{0} min ago", ["HoursAgo"] = "{0} h ago", ["DaysAgo"] = "{0} d ago",
        ["WindowUsed"] = "{0} {1}%", ["SessionsMissing"] = "Codex is not installed or has no sessions",
        ["NoSnapshots"] = "Waiting for a Codex usage snapshot", ["ReadError"] = "Temporary session read error", ["AccessDenied"] = "Codex sessions cannot be accessed"
    };

    private static readonly IReadOnlyDictionary<string, string> Es = new Dictionary<string, string>
    {
        ["Searching"] = "Codex Usage Meter: buscando datos...", ["StartWindows"] = "Iniciar con Windows",
        ["ShowPinned"] = "Mostrar widget fijo", ["Available"] = "Disponible: {0}%  ·  Usado: {1}%",
        ["ResetAt"] = "Se reinicia: {0}", ["NoReset"] = "Reinicio: sin datos", ["NoUsage"] = "No hay datos de uso",
        ["RunTask"] = "Abre Codex y ejecuta al menos una tarea", ["Refresh"] = "Actualizar ahora",
        ["KeepTray"] = "Mostrar siempre en la bandeja...",
        ["Notifications"] = "Notificaciones", ["NotifyChange"] = "Al cambiar el porcentaje",
        ["Notify50"] = "Al alcanzar 50 % usado", ["Notify75"] = "Al alcanzar 75 % usado", ["Notify90"] = "Al alcanzar 90 % usado",
        ["NotifyReset"] = "Al restablecerse el límite", ["Exit"] = "Salir", ["Widget"] = "Widget",
        ["Disabled"] = "Desactivado", ["Normal"] = "Normal", ["Compact"] = "Compacto", ["UsageBar"] = "Barra de uso",
        ["UsageBarEnabled"] = "Activada", ["Thickness"] = "Grosor",
        ["Display"] = "Pantalla", ["Automatic"] = "Automática", ["AllDisplays"] = "Todas las pantallas", ["Primary"] = "Principal",
        ["Language"] = "Idioma", ["UsageChanged"] = "El uso de Codex ha cambiado",
        ["Threshold"] = "Codex ha alcanzado {0}% de uso", ["LimitReset"] = "El límite de Codex se ha restablecido",
        ["BalloonUsage"] = "Uso de Codex", ["BalloonUnavailable"] = "Uso de Codex no disponible",
        ["BalloonNoData"] = "Ejecuta una tarea en Codex y vuelve a actualizar.", ["AvailableUsed"] = "{0}% disponible ({1}% usado).",
        ["Pin"] = "Fijar widget", ["Unpin"] = "Soltar widget", ["NoData"] = "Sin datos",
        ["WaitingTask"] = "Ejecuta una tarea en Codex", ["AutoUpdate"] = "Actualizaremos la tarjeta automáticamente",
        ["ResetsUnknown"] = "Resets disponibles: sin datos", ["AvailableText"] = "{0}% disponible",
        ["UsedText"] = "{0}% usado", ["Updated"] = "Actualizado {0}", ["Resets"] = "Resets disponibles: {0}",
        ["ResetPending"] = "Reinicio del uso pendiente", ["ResetUnderDay"] = "Reinicio del uso en menos de 1 día",
        ["ResetOneDay"] = "Reinicio del uso en 1 día", ["ResetDays"] = "Reinicio del uso en {0} días",
        ["WindowResets"] = "{0}: {1}", ["ResetNow"] = "ahora",
        ["OneReset"] = "1 reset disponible", ["ManyResets"] = "{0} resets disponibles",
        ["Credits"] = "Créditos: {0}", ["NoCredits"] = "Créditos: sin datos", ["Stale"] = "Desactualizado",
        ["JustNow"] = "ahora", ["MinutesAgo"] = "hace {0} min", ["HoursAgo"] = "hace {0} h", ["DaysAgo"] = "hace {0} d",
        ["WindowUsed"] = "{0} {1}%", ["SessionsMissing"] = "Codex no está instalado o no tiene sesiones",
        ["NoSnapshots"] = "Esperando datos de uso de Codex", ["ReadError"] = "Error temporal al leer las sesiones", ["AccessDenied"] = "No se puede acceder a las sesiones de Codex"
    };

    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo(English);
    public static string CurrentLanguage => Culture.Name;
    public static string Get(string key, params object?[] args)
    {
        var catalog = Culture.TwoLetterISOLanguageName == "es" ? Es : En;
        var value = catalog[key];
        return args.Length == 0 ? value : string.Format(Culture, value, args);
    }
    public static void SetLanguage(string language)
    {
        Culture = CultureInfo.GetCultureInfo(language.Equals(Spanish, StringComparison.OrdinalIgnoreCase) ? Spanish : English);
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
    }
    public static string DetectLanguage() => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? Spanish : English;
    public static bool CatalogsMatch() => En.Keys.Order().SequenceEqual(Es.Keys.Order());
}

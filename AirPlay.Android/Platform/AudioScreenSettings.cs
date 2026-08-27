using Android.Content;

namespace AirPlay.Android.Platform;

internal static class AudioScreenSettings
{
    private const string PreferencesName = "tcl_airplay_ui";
    private const string EnabledKey = "show_audio_now_playing";

    public static bool IsEnabled(Context context) =>
        context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)
            ?.GetBoolean(EnabledKey, false) ?? false;

    public static void Save(Context context, bool enabled) =>
        context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)
            ?.Edit()?.PutBoolean(EnabledKey, enabled)?.Apply();
}

using System;
using System.Linq;
using Android.Content;

namespace AirPlay.Android.Platform;

internal static class ReceiverNameSettings
{
    public const string DefaultBaseName = "TCL G03";
    public const string AudioSuffix = "Audio";
    public const string VideoSuffix = "Video";

    private const string PreferencesName = "tcl_airplay_settings";
    private const string BaseNameKey = "receiver_base_name";
    private const int MaxBaseNameLength = 32;

    public static string GetBaseName(Context context)
    {
        var preferences = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
        return Normalize(preferences?.GetString(BaseNameKey, DefaultBaseName));
    }

    public static string SaveBaseName(Context context, string? value)
    {
        var normalized = Normalize(value);
        var preferences = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private);
        preferences?.Edit()?.PutString(BaseNameKey, normalized)?.Apply();
        return normalized;
    }

    public static string AudioName(string baseName) => $"{Normalize(baseName)} {AudioSuffix}";

    public static string VideoName(string baseName) => $"{Normalize(baseName)} {VideoSuffix}";

    private static string Normalize(string? value)
    {
        var cleaned = new string((value ?? string.Empty)
            .Where(character => !char.IsControl(character))
            .ToArray());
        var words = cleaned.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var normalized = string.Join(" ", words).Trim();
        if (normalized.Length > MaxBaseNameLength)
        {
            normalized = normalized[..MaxBaseNameLength].TrimEnd();
        }

        return string.IsNullOrWhiteSpace(normalized) ? DefaultBaseName : normalized;
    }
}

using System;
using System.Globalization;

namespace HTFMod;

public enum ModLang
{
    Auto,
    ZhHans,
    ZhHant,
    En,
}

/// <summary>
/// Minimal three-language UI strings core (Simplified Chinese / Traditional Chinese / English).
/// Call L.T(zhHans, zhHant, en) wherever a user-facing string is needed; it resolves
/// against the current language. Auto mode follows the OS UI culture.
/// </summary>
public static class L
{
    public static ModLang Setting { get; private set; } = ModLang.Auto;
    public static ModLang Current { get; private set; } = ModLang.ZhHans;

    /// <summary>Hook set by HtfPlugin to persist the setting whenever it changes.</summary>
    public static Action<string> SaveHook;

    /// <summary>Apply a language setting (Auto resolves via the OS culture).</summary>
    public static void Apply(ModLang setting)
    {
        Setting = setting;
        Current = Resolve(setting);
        SaveHook?.Invoke(SettingKey(setting));
    }

    /// <summary>Apply a language by config key: auto / zh-hans / zh-hant / en.</summary>
    public static void ApplyKey(string key)
    {
        ModLang lang;
        switch (key)
        {
            case "zh-hans": lang = ModLang.ZhHans; break;
            case "zh-hant": lang = ModLang.ZhHant; break;
            case "en": lang = ModLang.En; break;
            default: lang = ModLang.Auto; break;
        }
        Apply(lang);
    }

    /// <summary>Config key for a language setting.</summary>
    public static string SettingKey(ModLang lang)
    {
        switch (lang)
        {
            case ModLang.ZhHans: return "zh-hans";
            case ModLang.ZhHant: return "zh-hant";
            case ModLang.En: return "en";
            default: return "auto";
        }
    }

    /// <summary>Cycle order for the in-menu language button.</summary>
    public static string NextKey(string key)
    {
        switch (key)
        {
            case "auto": return "zh-hans";
            case "zh-hans": return "zh-hant";
            case "zh-hant": return "en";
            default: return "auto";
        }
    }

    /// <summary>Display name for a config key.</summary>
    public static string DisplayName(string key)
    {
        switch (key)
        {
            case "zh-hans": return "简体中文";
            case "zh-hant": return "繁體中文";
            case "en": return "English";
            default: return "Auto";
        }
    }

    /// <summary>Resolve a user-facing string in the current language.</summary>
    public static string T(string zhHans, string zhHant, string en)
    {
        if (Current == ModLang.ZhHant) return zhHant;
        if (Current == ModLang.En) return en;
        return zhHans;
    }

    private static ModLang Resolve(ModLang setting)
    {
        if (setting != ModLang.Auto)
            return setting;
        try
        {
            var c = CultureInfo.CurrentUICulture ?? CultureInfo.CurrentCulture;
            var name = c != null ? c.Name : "";
            if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                if (name.IndexOf("hant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("-tw", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("-hk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("-mo", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ModLang.ZhHant;
                return ModLang.ZhHans;
            }
            return ModLang.En;
        }
        catch
        {
            return ModLang.ZhHans;
        }
    }
}

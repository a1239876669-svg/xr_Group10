using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public enum ApartmentDisplayLanguage { English = 0, SimplifiedChinese = 1 }

// Display-only catalog. No scene manager, record mutation, schema change or input ownership.
public static partial class ApartmentLanguage
{
    // Legacy key is intentionally neither read nor changed. Language belongs to this runtime only.
    public const string PreferenceKey = "COMP5424.Group10.DisplayLanguage.v1";
    sealed class Entry { public string English, Chinese; }
    static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
    static readonly Dictionary<string, string> aliases = new Dictionary<string, string>(StringComparer.Ordinal);
    static readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);
    static Regex copyPattern;
    static ApartmentDisplayLanguage current;
    public static event Action Changed;
    public static ApartmentDisplayLanguage Current => current;
    public static bool IsChinese => current == ApartmentDisplayLanguage.SimplifiedChinese;
    public static string FullTextCoverage => string.Concat(entries.Values.Select(e => e.English + e.Chinese));
    static ApartmentLanguage() { RegisterCatalog(); RegisterJourney(); current = ApartmentDisplayLanguage.English; }
    static partial void RegisterCatalog();
    static partial void RegisterJourney();
    public static void Register(string key, string english, string chinese, string legacy = null)
    {
        entries[key] = new Entry { English = english, Chinese = chinese };
        foreach (string value in new[] { english, chinese, legacy })
            if (!string.IsNullOrEmpty(value)) aliases[value] = key;
        copyPattern = null; cache.Clear();
    }
    public static string Text(string key) => entries.TryGetValue(key, out var e) ? (IsChinese ? e.Chinese : e.English) : key;
    public static string Format(string key, params object[] args) => string.Format(System.Globalization.CultureInfo.InvariantCulture, Text(key), args);
    // Only authored UI copy passes this boundary. Participants' text uses LabelRaw instead.
    public static string Display(string copy)
    {
        if (string.IsNullOrEmpty(copy)) return copy ?? "";
        if (cache.TryGetValue(copy, out var result)) return result;
        if (aliases.TryGetValue(copy, out var exact)) result = Text(exact);
        else
        {
            if (copyPattern == null)
                copyPattern = new Regex(string.Join("|", aliases.Keys.OrderByDescending(s => s.Length).Select(s =>
                    (char.IsLetter(s[0]) && s[0] < 128 ? @"(?<![A-Za-z])" : "") + Regex.Escape(s) +
                    (char.IsLetter(s[s.Length-1]) && s[s.Length-1] < 128 ? @"(?![A-Za-z])" : ""))), RegexOptions.CultureInvariant);
            result = copyPattern.Replace(copy, m => Text(aliases[m.Value]));
        }
        if (cache.Count > 2048) cache.Clear(); cache[copy] = result; return result;
    }
    public static void SetLanguage(ApartmentDisplayLanguage language)
    {
        if (!Enum.IsDefined(typeof(ApartmentDisplayLanguage), language)) throw new ArgumentOutOfRangeException(nameof(language));
        bool changed = current != language; current = language;
        cache.Clear();
        if (changed) Changed?.Invoke();
    }
    // Runs on every Player startup and every Play, including when Domain Reload is disabled.
    // Preserve active listeners: disabling Scene Reload may leave the existing panels alive.
    // SetLanguage refreshes those panels without resetting their journey or review state.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void BeginRun() { SetLanguage(ApartmentDisplayLanguage.English); }
}

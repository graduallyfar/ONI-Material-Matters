using System;

namespace MaterialOverhaul.Game
{
    internal static class PowerText
    {
        internal static string Get(string suffix, string fallback)
        {
            string key = "STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL." + suffix;
            string text = Strings.Get(key);
            return string.IsNullOrEmpty(text) || text.StartsWith("MISSING.", StringComparison.Ordinal) || text == key ? fallback : text;
        }
    }
}

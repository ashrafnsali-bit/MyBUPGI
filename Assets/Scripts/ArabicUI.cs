using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central helper for Arabic UI text. The strings in ArabicStrings are already
/// reshaped and reordered for Unity's legacy UI Text renderer, so all we need
/// here is a font that contains the Arabic Presentation Forms glyphs.
/// </summary>
public static class ArabicUI
{
    private static Font regularFont;
    private static Font boldFont;

    public static Font Get(bool bold = false)
    {
        if (bold)
        {
            if (boldFont == null)
                boldFont = Resources.Load<Font>("Fonts/NotoSansArabic-Bold");
            if (boldFont != null)
                return boldFont;
        }
        else
        {
            if (regularFont == null)
                regularFont = Resources.Load<Font>("Fonts/NotoSansArabic-Regular");
            if (regularFont != null)
                return regularFont;
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    public static void Apply(Text text, bool bold = false)
    {
        if (text != null)
            text.font = Get(bold);
    }

    public static string Format(string template, params object[] args)
    {
        if (string.IsNullOrEmpty(template) || args == null || args.Length == 0)
            return template;
        return string.Format(template, args);
    }
}

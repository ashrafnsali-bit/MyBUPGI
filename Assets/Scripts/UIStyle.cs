using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Procedural modern UI styling helper: generates crisp 9-sliced rounded-rectangle sprites
/// in memory so buttons, cards, and HUD panels have smooth rounded corners, subtle borders,
/// soft shadows, and responsive hover/press states without external image assets.
/// </summary>
public static class UIStyle
{
    private static Sprite roundedSprite;
    private static Sprite pillSprite;

    public static Sprite GetRoundedSprite()
    {
        if (roundedSprite == null)
            roundedSprite = CreateRoundedRectSprite(64, 64, 14);
        return roundedSprite;
    }

    public static Sprite GetPillSprite()
    {
        if (pillSprite == null)
            pillSprite = CreateRoundedRectSprite(64, 64, 24);
        return pillSprite;
    }

    private static Sprite CreateRoundedRectSprite(int width, int height, int radius)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[width * height];
        float r = radius;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = 0f;
                float dy = 0f;

                if (x < r) dx = r - x;
                else if (x >= width - r) dx = x - (width - r - 1);

                if (y < r) dy = r - y;
                else if (y >= height - r) dy = y - (height - r - 1);

                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(r - dist + 0.5f);

                // Subtle vertical sheen (slightly brighter at top) for depth
                float shade = Mathf.Lerp(0.90f, 1.0f, (float)y / (height - 1));
                pixels[y * width + x] = new Color(shade, shade, shade, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        Vector4 border = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    /// <summary>Applies rounded corners + dark glass fill + subtle shadow to any panel Image.</summary>
    public static void StyleCard(Image img, Color bgColor, bool addShadow = true)
    {
        if (img == null) return;
        img.sprite = GetRoundedSprite();
        img.type = Image.Type.Sliced;
        img.color = bgColor;

        if (addShadow && img.GetComponent<Shadow>() == null)
        {
            Shadow sh = img.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.45f);
            sh.effectDistance = new Vector2(0f, -4f);
        }
    }

    /// <summary>Styles a Button with rounded corners, hover/press color transitions, and drop shadow.</summary>
    public static void StyleButton(Button btn, Image img, Color baseColor, bool pill = false)
    {
        if (img != null)
        {
            img.sprite = pill ? GetPillSprite() : GetRoundedSprite();
            img.type = Image.Type.Sliced;
            img.color = Color.white; // ColorBlock tints the image cleanly
        }

        if (btn != null)
        {
            ColorBlock cb = btn.colors;
            cb.normalColor = baseColor;
            cb.highlightedColor = Color.Lerp(baseColor, Color.white, 0.18f);
            cb.pressedColor = Color.Lerp(baseColor, Color.black, 0.22f);
            cb.selectedColor = cb.highlightedColor;
            cb.disabledColor = new Color(0.28f, 0.32f, 0.38f, 0.75f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            btn.transition = Selectable.Transition.ColorTint;
        }

        if (img != null && img.GetComponent<Shadow>() == null)
        {
            Shadow sh = img.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.5f);
            sh.effectDistance = new Vector2(0f, -4f);
        }
    }

    /// <summary>Configures Arabic UI Text so tall Arabic glyphs never get silently clipped by Unity.</summary>
    public static void ConfigureArabicText(Text text, int fontSize, Color color, bool bold = true, TextAnchor align = TextAnchor.MiddleCenter, bool addShadow = true)
    {
        if (text == null) return;
        ArabicUI.Apply(text, bold);
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        if (addShadow && text.GetComponent<Shadow>() == null && text.GetComponent<Outline>() == null)
        {
            Shadow sh = text.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.75f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);
        }
    }
}

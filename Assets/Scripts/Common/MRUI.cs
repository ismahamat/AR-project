using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class MRUI
{
    public static readonly Color BG_CREAM       = new Color(0.96f, 0.94f, 0.88f, 0.98f);
    public static readonly Color TILE_WHITE     = new Color(1.00f, 1.00f, 1.00f, 1.00f);
    public static readonly Color TILE_SHADOW    = new Color(0.00f, 0.00f, 0.00f, 0.18f);
    public static readonly Color SELECTION_BLUE = new Color(0.13f, 0.67f, 1.00f, 1.00f);
    public static readonly Color TEXT_DARK      = new Color(0.13f, 0.16f, 0.22f, 1.00f);
    public static readonly Color TEXT_GRAY      = new Color(0.42f, 0.47f, 0.54f, 1.00f);
    public static readonly Color BUTTON_BLUE    = new Color(0.13f, 0.57f, 0.96f, 1.00f);
    public static readonly Color BUTTON_GREEN   = new Color(0.20f, 0.72f, 0.40f, 1.00f);
    public static readonly Color BUTTON_DISABLE = new Color(0.65f, 0.68f, 0.72f, 1.00f);
    public static readonly Color BG_TOP_FADE    = new Color(0.99f, 0.98f, 0.95f, 0.98f);

    static TMP_FontAsset _font;

    public static TMP_FontAsset Font
    {
        get
        {
            if (_font != null) return _font;
            _font = TMP_Settings.defaultFontAsset;
            if (_font != null) return _font;
            var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (all != null && all.Length > 0) _font = all[0];
            return _font;
        }
    }

    public static GameObject Panel(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    public static Image NewImage(string name, Transform parent, Color color, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        img.type = Image.Type.Sliced;
        return img;
    }

    public static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (Font != null) tmp.font = Font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.richText = true;
        return tmp;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    public static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    public static Color Lighten(Color c, float t) => Color.Lerp(c, Color.white, Mathf.Clamp01(t));
    public static Color Darken(Color c, float t)  => Color.Lerp(c, Color.black, Mathf.Clamp01(t));

    public static Sprite RoundedRect(int w, int h, int radius, Color fill)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = 0, dy = 0;
            if (x < radius) dx = radius - x - 0.5f;
            else if (x > w - radius - 1) dx = x - (w - radius - 1) - 0.5f;
            if (y < radius) dy = radius - y - 0.5f;
            else if (y > h - radius - 1) dy = y - (h - radius - 1) - 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(radius - d);
            var c = fill; c.a *= a;
            px[y * w + x] = c;
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    public static Sprite RoundedRing(int w, int h, int radius, int thickness, Color fill)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = 0, dy = 0;
            if (x < radius) dx = radius - x - 0.5f;
            else if (x > w - radius - 1) dx = x - (w - radius - 1) - 0.5f;
            if (y < radius) dy = radius - y - 0.5f;
            else if (y > h - radius - 1) dy = y - (h - radius - 1) - 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float aOuter = Mathf.Clamp01(radius - d);
            float aInner = Mathf.Clamp01((radius - thickness) - d);
            float a = Mathf.Clamp01(aOuter - aInner);
            var c = fill; c.a *= a;
            px[y * w + x] = c;
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    public static Sprite SoftShadow(int w, int h, int radius, int blur, Color fill)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = 0, dy = 0;
            if (x < radius + blur) dx = (radius + blur) - x - 0.5f;
            else if (x > w - radius - blur - 1) dx = x - (w - radius - blur - 1) - 0.5f;
            if (y < radius + blur) dy = (radius + blur) - y - 0.5f;
            else if (y > h - radius - blur - 1) dy = y - (h - radius - blur - 1) - 0.5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float t = Mathf.Clamp01((radius + blur - d) / (float)(blur));
            t = t * t * (3f - 2f * t);
            var c = fill; c.a *= t;
            px[y * w + x] = c;
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius + blur, radius + blur, radius + blur, radius + blur));
    }
}

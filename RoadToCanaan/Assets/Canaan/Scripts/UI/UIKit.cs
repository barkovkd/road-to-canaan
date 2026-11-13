using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Canaan
{
    /// <summary>Builds the interface from code: canvas, fonts, generated sprites and small layout helpers.</summary>
    public static class UI
    {
        public static readonly Color Ink = new Color32(0x1E, 0x1A, 0x16, 0xD8);
        public static readonly Color InkSolid = new Color32(0x1E, 0x1A, 0x16, 0xFF);
        public static readonly Color Paper = new Color32(0xE9, 0xDF, 0xC6, 0xFF);
        public static readonly Color Muted = new Color32(0xB3, 0xA7, 0x8C, 0xFF);
        public static readonly Color Dim = new Color32(0x7A, 0x70, 0x5E, 0xFF);
        public static readonly Color Accent = new Color32(0xC9, 0x41, 0x2B, 0xFF);
        public static readonly Color Health = new Color32(0xC2, 0x3B, 0x2E, 0xFF);
        public static readonly Color Stamina = new Color32(0xD9, 0xA4, 0x41, 0xFF);
        public static readonly Color Cool = new Color32(0x86, 0xB0, 0xBC, 0xFF);
        public static readonly Color Good = new Color32(0x8F, 0xA8, 0x5A, 0xFF);

        public static Canvas canvas;
        public static RectTransform root;
        public static Font font;
        public static Sprite white, rounded, roundedSmall, circle, ring, diamond, arrow, soft;
        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

        public static void Init(Transform parent, Camera cam)
        {
            var go = new GameObject("UI") { layer = Layers.UI };
            go.transform.SetParent(parent, false);
            canvas = go.AddComponent<Canvas>();
            if (Args.Offscreen)
            {
                // tests render the camera on demand, so the interface has to be part of that camera's picture
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 0.4f;
            }
            else canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            root = (RectTransform)go.transform;

            font = Font.CreateDynamicFontFromOSFont(new[] { "PT Serif", "Georgia", "Times New Roman", "Arial" }, 32);
            white = MakeSprite(8, 8, (x, y) => 1f, 0);
            rounded = MakeSprite(48, 48, (x, y) => RoundBox(x, y, 48, 48, 12f), 16);
            roundedSmall = MakeSprite(24, 24, (x, y) => RoundBox(x, y, 24, 24, 5f), 8);
            circle = MakeSprite(96, 96, (x, y) => Mathf.Clamp01(47f - Dist(x, y, 48f, 48f)), 0);
            ring = MakeSprite(96, 96, (x, y) => Mathf.Clamp01(47f - Dist(x, y, 48f, 48f)) * Mathf.Clamp01(Dist(x, y, 48f, 48f) - 38f), 0);
            soft = MakeSprite(64, 64, (x, y) => U.Smooth01(1f - Dist(x, y, 32f, 32f) / 32f), 0);
            diamond = MakeSprite(48, 48, (x, y) => Mathf.Clamp01(22f - (Mathf.Abs(x - 24f) + Mathf.Abs(y - 24f))), 0);
            arrow = MakeSprite(64, 64, (x, y) =>
            {
                // a pointer: triangle with a notch at the back, pointing up
                float fx = Mathf.Abs(x - 32f), fy = y - 8f;
                float edge = (48f - fy) * 0.42f - fx;
                float notch = fy - 10f + fx * 0.6f;
                return Mathf.Clamp01(Mathf.Min(edge, notch)) * (fy >= 0f && fy <= 48f ? 1f : 0f);
            }, 0);
        }

        static float Dist(float x, float y, float cx, float cy) { return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)); }

        static float RoundBox(float x, float y, float w, float h, float r)
        {
            float dx = Mathf.Max(Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r), 0f), dy = Mathf.Max(Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r), 0f);
            return Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
        }

        /// <summary>A white sprite whose alpha comes from a coverage function sampled at pixel centres.</summary>
        public static Sprite MakeSprite(int w, int h, Func<float, float, float> alpha, int border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f)) * 255f));
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        /// <summary>Small pictograms drawn from simple shapes.</summary>
        public static Sprite Icon(string name)
        {
            if (icons.TryGetValue(name, out Sprite s)) return s;
            Func<float, float, float> f;
            switch (name)
            {
                case "heart":
                    f = (x, y) =>
                    {
                        float a = Mathf.Clamp01(15f - Dist(x, y, 20f, 39f)), b = Mathf.Clamp01(15f - Dist(x, y, 44f, 39f));
                        float t = Mathf.Clamp01(Mathf.Min((y - 8f) * 0.95f - Mathf.Abs(x - 32f) + 6f, 40f - y));
                        return Mathf.Max(Mathf.Max(a, b), t);
                    };
                    break;
                case "bolt":
                    f = (x, y) =>
                    {
                        float top = y > 30f ? Mathf.Clamp01(8f - Mathf.Abs((x - 24f) - (y - 30f) * 0.45f)) : 0f;
                        float bot = y <= 34f ? Mathf.Clamp01(8f - Mathf.Abs((x - 40f) + (34f - y) * 0.45f)) : 0f;
                        return Mathf.Max(top, bot) * (y > 6f && y < 58f ? 1f : 0f);
                    };
                    break;
                case "eye":
                    f = (x, y) =>
                    {
                        float lid = Mathf.Clamp01(26f - Dist(x, y, 32f, 14f)) * Mathf.Clamp01(26f - Dist(x, y, 32f, 50f));
                        float pupil = Mathf.Clamp01(7f - Dist(x, y, 32f, 32f));
                        return Mathf.Clamp01(lid - pupil * 1.2f) + Mathf.Clamp01(3.5f - Dist(x, y, 32f, 32f));
                    };
                    break;
                case "horse":
                    f = (x, y) =>
                    {
                        // horseshoe
                        float d = Dist(x, y, 32f, 36f);
                        float arc = Mathf.Clamp01(22f - d) * Mathf.Clamp01(d - 13f) * (y > 20f ? 1f : 0f);
                        float legs = Mathf.Clamp01(4.5f - Mathf.Abs(Mathf.Abs(x - 32f) - 17.5f)) * (y <= 36f && y > 10f ? 1f : 0f);
                        return Mathf.Max(arc, legs);
                    };
                    break;
                case "coin":
                    f = (x, y) => Mathf.Clamp01(24f - Dist(x, y, 32f, 32f)) * (1f - 0.75f * Mathf.Clamp01(18f - Dist(x, y, 32f, 32f)) * Mathf.Clamp01(Dist(x, y, 32f, 32f) - 14f));
                    break;
                case "star":
                    f = (x, y) =>
                    {
                        float dx = x - 32f, dy = y - 32f;
                        float ang = Mathf.Atan2(dx, dy), r = Mathf.Sqrt(dx * dx + dy * dy);
                        float k = Mathf.Abs(Mathf.Repeat(ang / (Mathf.PI * 2f) * 5f, 1f) - 0.5f) * 2f;
                        return Mathf.Clamp01(Mathf.Lerp(26f, 11f, k) - r);
                    };
                    break;
                case "cross":
                    f = (x, y) => Mathf.Clamp01(5f - Mathf.Min(Mathf.Abs((x - 32f) - (y - 32f)), Mathf.Abs((x - 32f) + (y - 32f))) * 0.7f) * Mathf.Clamp01(26f - Dist(x, y, 32f, 32f));
                    break;
                case "tent":
                    f = (x, y) => Mathf.Clamp01((y - 10f) > 0f ? (44f - (y - 10f)) * 0.62f - Mathf.Abs(x - 32f) : 0f) * (1f - Mathf.Clamp01((30f - y) * 0.4f - Mathf.Abs(x - 32f) + 1f));
                    break;
                case "house":
                    f = (x, y) => Mathf.Max(Mathf.Clamp01(Mathf.Min(18f - Mathf.Abs(x - 32f), Mathf.Min(y - 10f, 34f - y))),
                                            Mathf.Clamp01(y >= 32f ? (56f - y) * 1.05f - Mathf.Abs(x - 32f) : 0f));
                    break;
                default:
                    f = (x, y) => Mathf.Clamp01(20f - Dist(x, y, 32f, 32f));
                    break;
            }
            s = MakeSprite(64, 64, f, 0);
            icons[name] = s;
            return s;
        }

        // ------------------------------------------------------------ elements

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = Layers.UI };
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Img(Transform parent, Sprite sprite, Color color, string name = "Img")
        {
            RectTransform rt = Node(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null && sprite.border.sqrMagnitude > 0f) img.type = Image.Type.Sliced;
            return img;
        }

        public static Image Bar(Transform parent, Sprite sprite, Color color, string name = "Bar")
        {
            Image img = Img(parent, sprite, color, name);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = 0;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, bool bold = false, string name = "Text")
        {
            RectTransform rt = Node(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static Text Shadowed(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, bool bold = false)
        {
            // an outline keeps text readable over sand and sky alike
            Text t = Label(parent, text, size, color, align, bold);
            var ol = t.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.8f);
            ol.effectDistance = new Vector2(1.6f, -1.6f);
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
            sh.effectDistance = new Vector2(2f, -3f);
            return t;
        }

        /// <summary>Anchors an element to a corner or edge (ax, ay in 0..1) at an offset, with a fixed size.</summary>
        public static RectTransform Place(Component c, float ax, float ay, float x, float y, float w, float h)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform PlaceCentre(Component c, float ax, float ay, float x, float y, float w, float h)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Stretch(Component c, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Converts a world point to canvas coordinates (origin at the screen centre). False if behind the camera.</summary>
        public static bool WorldToCanvas(Camera cam, Vector3 world, out Vector2 pos)
        {
            Vector3 v = cam.WorldToViewportPoint(world);
            Vector2 size = root.rect.size;
            pos = new Vector2((v.x - 0.5f) * size.x, (v.y - 0.5f) * size.y);
            return v.z > 0.05f;
        }

        /// <summary>Mouse position in canvas coordinates (origin at the centre).</summary>
        public static Vector2 PointerOnCanvas()
        {
            Vector2 size = root.rect.size;
            float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
            return new Vector2((GameInput.Pointer.x / sw - 0.5f) * size.x, (GameInput.Pointer.y / sh - 0.5f) * size.y);
        }

        public static string Key(Btn b) { return "<color=#E9DFC6><b>[" + GameInput.Label(b) + "]</b></color>"; }
    }
}

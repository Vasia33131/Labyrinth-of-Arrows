using UnityEngine;

namespace Unpuzzle
{
    /// <summary>
    /// Гарантирует видимый спрайт даже если PNG не импортировался.
    /// </summary>
    public static class ArrowSpriteFactory
    {
        /// <summary>Доля ширины текстуры под треугольник — по краям остаётся запас на сглаживание.</summary>
        public const float TriangleWidthNormalized = 0.92f;

        /// <summary>Высота считается из пропорции наконечника в клетках, чтобы масштаб был равномерным.</summary>
        public static readonly float TriangleHeightNormalized =
            TriangleWidthNormalized * GameConstants.ArrowHeadLengthInCells / GameConstants.ArrowHeadWidthInCells;

        /// <summary>Пивот на основании треугольника — в эту точку приходит конец линии пути.</summary>
        public static readonly float TrianglePivotY = (1f - TriangleHeightNormalized) * 0.5f;

        private static Sprite cachedArrow;
        private static Sprite cachedSquare;
        private static Sprite cachedTriangle;

        public static Sprite GetArrow()
        {
            if (cachedArrow != null) return cachedArrow;
            cachedArrow = Sprite.Create(MakeArrowTexture(), new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f), 128f);
            cachedArrow.name = "RuntimeArrow";
            return cachedArrow;
        }

        public static Sprite GetSquare()
        {
            if (cachedSquare != null) return cachedSquare;
            cachedSquare = Sprite.Create(MakeSquareTexture(), new Rect(0f, 0f, 128f, 128f), new Vector2(0.5f, 0.5f), 128f);
            cachedSquare.name = "RuntimeSquare";
            return cachedSquare;
        }

        /// <summary>Треугольник наконечника, остриё вверх, пивот на основании.</summary>
        public static Sprite GetTriangle()
        {
            if (cachedTriangle != null) return cachedTriangle;

            const int size = 256;
            Texture2D tex = MakeTriangleTexture(size);
            cachedTriangle = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, TrianglePivotY), size);
            cachedTriangle.name = "RuntimeTriangle";
            return cachedTriangle;
        }

        public static Sprite GetColored(ArrowColor color)
        {
            // Плоская заливка: белый треугольник + tint из ColorToUnityColor.
            // Стеклянные PNG из ArtLibrary нарочно не берём.
            _ = color;
            return GetTriangle();
        }

        public static void EnsureSprite(SpriteRenderer renderer, bool square = false)
        {
            if (renderer == null) return;
            if (renderer.sprite != null) return;
            renderer.sprite = square ? GetSquare() : GetTriangle();
        }

        public static Texture2D MakeTriangleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };

            var pixels = new Color32[size * size];

            // Вершины поджаты на round, поэтому силуэт после скругления попадает точно в заданные габариты.
            const float round = 0.02f;
            float halfWidth = TriangleWidthNormalized * 0.5f;
            float baseY = TrianglePivotY;
            float tipY = baseY + TriangleHeightNormalized;
            var left = new Vector2(0.5f - halfWidth + round, baseY + round);
            var right = new Vector2(0.5f + halfWidth - round, baseY + round);
            var tip = new Vector2(0.5f, tipY - round);
            float edgeSoftness = 1.5f / size;

            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size;
                    float distance = SdTriangle(new Vector2(nx, ny), left, right, tip) - round;
                    float alpha = Mathf.Clamp01(0.5f - distance / edgeSoftness);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static float SdTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 e0 = b - a;
            Vector2 e1 = c - b;
            Vector2 e2 = a - c;
            Vector2 v0 = p - a;
            Vector2 v1 = p - b;
            Vector2 v2 = p - c;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            float dx = Mathf.Min(Vector2.Dot(pq0, pq0), Mathf.Min(Vector2.Dot(pq1, pq1), Vector2.Dot(pq2, pq2)));
            float dy = Mathf.Min(s * (v0.x * e0.y - v0.y * e0.x), Mathf.Min(s * (v1.x * e1.y - v1.y * e1.x), s * (v2.x * e2.y - v2.y * e2.x)));
            return -Mathf.Sqrt(dx) * Mathf.Sign(dy);
        }

        private static Texture2D MakeArrowTexture()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };

            var pixels = new Color32[size * size];
            var clear = new Color32(255, 255, 255, 0);
            var solid = new Color32(255, 255, 255, 255);

            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size;
                    bool inShaft = ny >= 0.08f && ny <= 0.58f && Mathf.Abs(nx - 0.5f) <= 0.13f;
                    bool inHead = false;
                    if (ny >= 0.52f && ny <= 0.94f)
                    {
                        float t = Mathf.InverseLerp(0.52f, 0.94f, ny);
                        float halfWidth = Mathf.Lerp(0.36f, 0.02f, t);
                        inHead = Mathf.Abs(nx - 0.5f) <= halfWidth;
                    }

                    pixels[y * size + x] = (inShaft || inHead) ? solid : clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static Texture2D MakeSquareTexture()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };

            var pixels = new Color32[size * size];
            var solid = new Color32(255, 255, 255, 255);
            var clear = new Color32(255, 255, 255, 0);
            int border = Mathf.RoundToInt(size * 0.06f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = x >= border && x < size - border && y >= border && y < size - border;
                    pixels[y * size + x] = inside ? solid : clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }
    }
}

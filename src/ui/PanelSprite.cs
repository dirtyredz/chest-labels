using UnityEngine;

namespace ChestLabels
{
    /// <summary>
    /// Generates the rounded, gold-edged plate behind the hover label.
    ///
    /// Built as a 9-sliced sprite: the corners keep their radius while the middle stretches,
    /// so one small texture serves any label length. A plain rectangle is the thing that makes
    /// modded UI look bolted on, and the game's own panels are all rounded with a lighter
    /// edge, so this follows suit.
    ///
    /// Palette taken from the game's chest window: deep plum fill, muted gold edge.
    /// </summary>
    internal static class PanelSprite
    {
        private const int Size = 48;          // texture is small; 9-slicing does the rest
        private const int Corner = 14;        // corner radius in pixels
        private const float EdgeWidth = 2.0f; // gold rim thickness

        // Matched to the game's own world nameplate (the "Chester" banner): a mid royal
        // purple with a darker rim, not the near-black plum of the inventory windows. The
        // nameplate is the closest thing the game has to a floating world label, so it is the
        // right thing to imitate.
        //
        // Estimated from a screenshot rather than sampled from the asset - close, but worth
        // re-checking against the real thing.
        private static readonly Color Fill = new Color32(0x5A, 0x2D, 0x91, 0xFF);
        private static readonly Color Edge = new Color32(0x3B, 0x1B, 0x63, 0xFF);

        private static Sprite cached;

        internal static Sprite Get()
        {
            if (cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "ChestLabels_Panel"
            };

            var pixels = new Color[Size * Size];

            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var distance = RoundedRectDistance(x + 0.5f, y + 0.5f);

                    // Negative inside the shape, positive outside; the band just inside the
                    // boundary becomes the rim.
                    var coverage = Mathf.Clamp01(0.5f - distance);
                    var rim = Mathf.Clamp01(distance + EdgeWidth) * Mathf.Clamp01(0.5f - distance);

                    var colour = Color.Lerp(Fill, Edge, Mathf.Clamp01(rim));
                    colour.a = coverage;

                    pixels[y * Size + x] = colour;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            // The border tells Unity which region stretches; corners are preserved.
            var border = new Vector4(Corner, Corner, Corner, Corner);
            cached = Sprite.Create(
                texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
            cached.name = "ChestLabels_Panel";
            return cached;
        }

        /// <summary>
        /// Signed distance to a rounded rectangle: negative inside, positive outside. Gives a
        /// clean antialiased edge without supersampling every pixel.
        /// </summary>
        private static float RoundedRectDistance(float x, float y)
        {
            var halfSize = Size * 0.5f;
            var innerHalf = halfSize - Corner;

            var dx = Mathf.Abs(x - halfSize) - innerHalf;
            var dy = Mathf.Abs(y - halfSize) - innerHalf;

            var outsideX = Mathf.Max(dx, 0f);
            var outsideY = Mathf.Max(dy, 0f);
            var outside = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY);

            var inside = Mathf.Min(Mathf.Max(dx, dy), 0f);

            return outside + inside - Corner;
        }
    }
}

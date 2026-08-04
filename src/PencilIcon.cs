using UnityEngine;

namespace ChestLabels
{
    /// <summary>
    /// Draws a pencil sprite at runtime.
    ///
    /// The game ships no pencil, quill or edit glyph - all 1,659 UI icons were checked - and
    /// its font has no pencil character either, which is why the button first showed a
    /// missing-glyph box and then a stray arrow. Generating the sprite keeps the mod a single
    /// DLL with no art files to ship alongside it.
    ///
    /// Styled to sit next to the game's own art: warm wood, a heavy dark outline, and the
    /// flat saturated fills the UI uses elsewhere.
    /// </summary>
    internal static class PencilIcon
    {
        private static Sprite cached;

        // Pencil-local axis: u runs tip -> eraser, v across the barrel.
        private const float TipEnd = -0.44f;      // where the cone meets the barrel
        private const float GraphiteEnd = -0.60f; // dark point vs bare wood on the cone
        private const float TipStart = -0.86f;
        private const float FerruleStart = 0.52f;
        private const float EraserStart = 0.68f;
        private const float EraserEnd = 0.86f;
        private const float HalfWidth = 0.17f;
        private const float Outline = 0.07f;

        // Brightened over the first pass: at 30px against a dark purple panel the original
        // palette read as a murky smudge rather than a pencil.
        private static readonly Color Wood = new Color32(0xFF, 0xCE, 0x85, 0xFF);
        private static readonly Color WoodShade = new Color32(0xE8, 0xA9, 0x5C, 0xFF);
        private static readonly Color BareWood = new Color32(0xFF, 0xEB, 0xC6, 0xFF);
        private static readonly Color Graphite = new Color32(0x5A, 0x4E, 0x6B, 0xFF);
        private static readonly Color Ferrule = new Color32(0xE2, 0xDD, 0xEC, 0xFF);
        private static readonly Color Eraser = new Color32(0xFF, 0x9A, 0xB8, 0xFF);
        private static readonly Color Ink = new Color32(0x2A, 0x1B, 0x3D, 0xFF);

        internal static Sprite Get(int size = 64)
        {
            if (cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "ChestLabels_Pencil"
            };

            const int samples = 3; // 3x3 supersampling; the diagonal edges alias badly without it
            var pixels = new Color[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var accumulated = new Vector4(0f, 0f, 0f, 0f);

                    for (var sy = 0; sy < samples; sy++)
                    {
                        for (var sx = 0; sx < samples; sx++)
                        {
                            var px = (x + (sx + 0.5f) / samples) / size * 2f - 1f;
                            var py = (y + (sy + 0.5f) / samples) / size * 2f - 1f;

                            var colour = Sample(px, py);
                            accumulated += new Vector4(
                                colour.r * colour.a, colour.g * colour.a, colour.b * colour.a, colour.a);
                        }
                    }

                    var total = samples * samples;
                    var alpha = accumulated.w / total;

                    pixels[y * size + x] = alpha <= 0.001f
                        ? new Color(0f, 0f, 0f, 0f)
                        // Un-premultiply so partially covered edge pixels keep their colour.
                        : new Color(accumulated.x / accumulated.w, accumulated.y / accumulated.w,
                                    accumulated.z / accumulated.w, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            cached = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            cached.name = "ChestLabels_Pencil";
            return cached;
        }

        /// <summary>Colour at a point in [-1,1] space, transparent outside the pencil.</summary>
        private static Color Sample(float px, float py)
        {
            // Lay the pencil along the 45-degree diagonal: tip low-left, eraser high-right.
            const float k = 0.70710678f; // cos/sin of 45 degrees
            var u = px * k + py * k;
            var v = -px * k + py * k;

            var fill = Body(u, v, 0f);
            if (fill.HasValue)
            {
                return fill.Value;
            }

            // Anything just outside the silhouette becomes the heavy dark outline that the
            // game's art uses throughout.
            return Body(u, v, Outline).HasValue ? Ink : new Color(0f, 0f, 0f, 0f);
        }

        private static Color? Body(float u, float v, float grow)
        {
            var half = HalfWidth + grow;

            if (u < TipStart - grow || u > EraserEnd + grow)
            {
                return null;
            }

            // Cone: half-width tapers to nothing at the point.
            if (u < TipEnd)
            {
                var t = Mathf.InverseLerp(TipStart - grow, TipEnd, u);
                if (Mathf.Abs(v) > half * t)
                {
                    return null;
                }
                return u < GraphiteEnd ? Graphite : BareWood;
            }

            if (Mathf.Abs(v) > half)
            {
                return null;
            }

            if (u >= EraserStart)
            {
                return Eraser;
            }

            if (u >= FerruleStart)
            {
                return Ferrule;
            }

            // A darker lower half suggests a rounded barrel without needing a gradient.
            return v < -HalfWidth * 0.25f ? WoodShade : Wood;
        }
    }
}


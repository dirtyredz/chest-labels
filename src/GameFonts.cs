using System;
using TMPro;
using UnityEngine;

namespace ChestLabels
{
    /// <summary>
    /// Locates the game's own typeface so mod text does not stand out as foreign.
    ///
    /// Moonlight Peaks uses <b>Gelica</b>, and ships material presets alongside it —
    /// Gelica-Bold-Outline and Gelica-Bold-Glow among them. The outline preset is what the
    /// game uses for text that has to read against the world, which is exactly the hover
    /// label's problem, so it is preferred over anything hand-rolled.
    ///
    /// Everything is looked up by name from already-loaded assets. Nothing is loaded from
    /// Addressables, so this cannot fail in a way that stalls or throws.
    /// </summary>
    internal static class GameFonts
    {
        private static bool searched;
        private static TMP_FontAsset font;
        private static Material outlineMaterial;

        /// <summary>The game's Gelica Bold font asset, or null if it could not be found.</summary>
        internal static TMP_FontAsset Font
        {
            get { Search(); return font; }
        }

        /// <summary>
        /// The game's outlined material preset for Gelica Bold. Null if unavailable, in which
        /// case the caller should fall back to TMP's own outline properties.
        /// </summary>
        internal static Material OutlineMaterial
        {
            get { Search(); return outlineMaterial; }
        }

        private static TMP_FontAsset heavyFont;
        private static bool heavySearched;

        /// <summary>
        /// The heaviest weight the game ships — Gelica-Black. Used for titles that need more
        /// presence than body text. Falls back to Bold.
        /// </summary>
        internal static TMP_FontAsset HeavyFont
        {
            get
            {
                if (heavySearched)
                {
                    return heavyFont ?? Font;
                }
                heavySearched = true;

                try
                {
                    foreach (var asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                    {
                        if (asset != null &&
                            string.Equals(asset.name, "Gelica-Black", StringComparison.OrdinalIgnoreCase))
                        {
                            heavyFont = asset;
                            break;
                        }
                    }

                    ChestLabelsPlugin.Log.LogInfo(
                        $"Heavy font: {(heavyFont == null ? "Gelica-Black not loaded, using Bold" : heavyFont.name)}");
                }
                catch (Exception e)
                {
                    ChestLabelsPlugin.Log.LogWarning($"Heavy font lookup failed: {e.Message}");
                }

                return heavyFont ?? Font;
            }
        }

        /// <summary>Apply the game's font, and its outline preset when one exists.</summary>
        internal static void Apply(TextMeshProUGUI text, bool preferOutline, bool heavy = false)
        {
            if (text == null)
            {
                return;
            }

            var resolved = heavy ? HeavyFont : Font;
            if (resolved != null)
            {
                text.font = resolved;
            }
            else if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            if (preferOutline && OutlineMaterial != null)
            {
                text.fontSharedMaterial = OutlineMaterial;
            }
        }

        private static void Search()
        {
            if (searched)
            {
                return;
            }
            searched = true;

            try
            {
                // Preference order: the weight the game's own UI headers use, then anything
                // else in the family, so a renamed asset still lands on Gelica.
                var preferred = new[] { "Gelica-Bold", "Gelica-Black", "Gelica-Regular", "Gelica" };

                var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                foreach (var candidate in preferred)
                {
                    foreach (var asset in fonts)
                    {
                        if (asset != null && string.Equals(asset.name, candidate, StringComparison.OrdinalIgnoreCase))
                        {
                            font = asset;
                            break;
                        }
                    }
                    if (font != null)
                    {
                        break;
                    }
                }

                if (font == null)
                {
                    foreach (var asset in fonts)
                    {
                        if (asset != null && asset.name.IndexOf("Gelica", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            font = asset;
                            break;
                        }
                    }
                }

                // Material presets are separate Materials, not font assets, so they need
                // their own pass.
                var materials = Resources.FindObjectsOfTypeAll<Material>();
                foreach (var name in new[] { "Gelica-Bold-Outline", "Gelica-Black-Outline", "Gelica-Bold-Glow" })
                {
                    foreach (var material in materials)
                    {
                        if (material != null && string.Equals(material.name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            outlineMaterial = material;
                            break;
                        }
                    }
                    if (outlineMaterial != null)
                    {
                        break;
                    }
                }

                ChestLabelsPlugin.Log.LogInfo(
                    $"Game font: {(font == null ? "not found, using TMP default" : font.name)}; " +
                    $"outline preset: {(outlineMaterial == null ? "none" : outlineMaterial.name)}");
            }
            catch (Exception e)
            {
                ChestLabelsPlugin.Log.LogWarning($"Font lookup failed, falling back to TMP default: {e.Message}");
            }
        }
    }
}

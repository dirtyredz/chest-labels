using System.Collections.Generic;
using Chicken.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChestLabels
{
    /// <summary>
    /// Shows the hover label through the game's own nameplate banner - the same one it uses for
    /// character names - so the font, colour, shape and animation are the game's and stay
    /// correct even if a patch restyles them.
    ///
    /// NameplateScreen is shared with the game's own tooltips, so the original colour of every
    /// image tinted is cached and restored the moment our label goes away. The screen keys
    /// Show/Hide by target RectTransform, so parking an invisible anchor at the chest's screen
    /// position is enough and we can never disturb a nameplate the game is showing for something
    /// else. Split out of <see cref="HoverLabel"/>.
    /// </summary>
    internal sealed class GameNameplateView
    {
        private readonly Dictionary<Image, Color> tintedOriginals = new Dictionary<Image, Color>();
        private string nameplateShownFor;

        /// <summary>Whether the game's nameplate screen is present to show through.</summary>
        internal static bool IsAvailable => UIScreen<NameplateScreen>.Instance != null;

        /// <summary>
        /// Show the label on the game nameplate anchored at <paramref name="anchor"/>. Only calls
        /// the game's Show when the chest or its name actually changes; calling it every poll
        /// would restart the reveal animation eight times a second.
        /// </summary>
        internal void Show(RectTransform anchor, string guid, string label)
        {
            var screen = UIScreen<NameplateScreen>.Instance;
            if (screen == null || anchor == null)
            {
                return;
            }

            var key = guid + " " + label;
            if (key == nameplateShownFor)
            {
                return;
            }

            nameplateShownFor = key;
            screen.Show(anchor, new CustomNameplateData(label));
            ApplyTint(screen);
        }

        /// <summary>Hide our nameplate (if showing) and restore any colours we tinted.</summary>
        internal void Hide(RectTransform anchor)
        {
            if (nameplateShownFor == null)
            {
                RestoreTint();
                return;
            }

            nameplateShownFor = null;
            RestoreTint();

            var screen = UIScreen<NameplateScreen>.Instance;
            if (screen != null && anchor != null)
            {
                screen.Hide(anchor, true);
            }
        }

        /// <summary>
        /// Recolour the game's nameplate bubble while it is showing our label. The pointer arrow
        /// is included: it is part of the bubble's silhouette, so leaving it untinted left an
        /// orange spike hanging off a purple bubble.
        /// </summary>
        private void ApplyTint(NameplateScreen screen)
        {
            var hex = ChestLabelsPlugin.NameplateTint.Value;
            if (string.IsNullOrWhiteSpace(hex) || !ColorUtility.TryParseHtmlString(hex, out var tint))
            {
                return;
            }

            foreach (var image in screen.GetComponentsInChildren<Image>(true))
            {
                if (image == null)
                {
                    continue;
                }

                if (!tintedOriginals.ContainsKey(image))
                {
                    tintedOriginals[image] = image.color;
                }

                // Preserve the bubble's own alpha so a fade-in still fades.
                tint.a = image.color.a;
                image.color = tint;
            }
        }

        private void RestoreTint()
        {
            if (tintedOriginals.Count == 0)
            {
                return;
            }

            foreach (var pair in tintedOriginals)
            {
                if (pair.Key != null)
                {
                    pair.Key.color = pair.Value;
                }
            }

            tintedOriginals.Clear();
        }
    }
}

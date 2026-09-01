using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChestLabels
{
    /// <summary>
    /// The mod's own hover-label plate: a rounded, gold-edged plate with centred text, built
    /// once on the shared overlay canvas. Used when the game nameplate is disabled
    /// (<c>UseGameNameplate=false</c>) or unavailable. Split out of <see cref="HoverLabel"/> so
    /// the mod-drawn view is separate from the game-nameplate view and the poll loop.
    /// </summary>
    internal sealed class HoverLabelPlateView
    {
        private const float OutlineWidth = 0.3f;
        private const float PlatePaddingX = 26f;
        private const float PlatePaddingY = 12f;
        private const float MinPlateWidth = 90f;
        private const float MaxPlateWidth = 420f;

        private RectTransform plateRect;
        private Image plateBackground;
        private TextMeshProUGUI text;

        /// <summary>Build the plate under the given canvas transform, once.</summary>
        internal void Ensure(Transform canvasParent)
        {
            if (plateRect != null)
            {
                return;
            }

            var plate = new GameObject("Plate");
            plate.transform.SetParent(canvasParent, false);

            plateRect = plate.AddComponent<RectTransform>();
            plateRect.sizeDelta = new Vector2(MinPlateWidth, 40f);
            plateRect.pivot = new Vector2(0.5f, 0.5f);

            plateBackground = plate.AddComponent<Image>();
            plateBackground.sprite = PanelSprite.Get();
            plateBackground.type = Image.Type.Sliced; // corners hold their radius as it stretches
            plateBackground.color = Color.white;
            plateBackground.raycastTarget = false;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(plate.transform, false);

            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 3f);
            textRect.offsetMax = new Vector2(-8f, -3f);

            text = textGo.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;

            // Warm gold on plum, matching the item counts in the game's own panels.
            text.color = GamePalette.NameCream;
            text.fontSize = ChestLabelsPlugin.HoverFontSize.Value;

            // The game's own typeface, with its outline preset where available - this label
            // has to read against whatever happens to be behind the chest.
            GameFonts.Apply(text, preferOutline: true);

            if (GameFonts.OutlineMaterial == null)
            {
                text.outlineWidth = OutlineWidth;
                text.outlineColor = GamePalette.Ink;
            }
        }

        /// <summary>Set the label and show the plate, restyling from config each time.</summary>
        internal void Show(string label)
        {
            if (text == null)
            {
                return;
            }

            text.text = label;

            // Re-applied on every show so tuning these in the .cfg takes effect without a
            // restart - the UI objects themselves are only built once.
            ApplyStyle();
            plateRect.gameObject.SetActive(true);
        }

        internal void Hide()
        {
            if (plateRect != null)
            {
                plateRect.gameObject.SetActive(false);
            }
        }

        internal void Reposition(Vector3 screenPoint)
        {
            if (plateRect != null)
            {
                plateRect.position = screenPoint;
            }
        }

        /// <summary>
        /// The vanilla interaction chevron sits directly above a chest, so the hover label
        /// defaults to no background and relies on its outline for legibility instead of
        /// stamping an opaque plate over the game's own indicator.
        /// </summary>
        private void ApplyStyle()
        {
            var alpha = Mathf.Clamp01(ChestLabelsPlugin.HoverBackgroundAlpha.Value);

            // Tint the generated sprite rather than replacing its colours, so the gold rim
            // fades together with the plum fill instead of separating from it.
            plateBackground.color = new Color(1f, 1f, 1f, alpha);
            plateBackground.enabled = alpha > 0.003f;

            text.fontSize = ChestLabelsPlugin.HoverFontSize.Value;

            // Size the plate to the name instead of shrinking long names to fit a fixed box.
            var preferred = text.GetPreferredValues(text.text);
            plateRect.sizeDelta = new Vector2(
                Mathf.Clamp(preferred.x + PlatePaddingX, MinPlateWidth, MaxPlateWidth),
                preferred.y + PlatePaddingY);
        }
    }
}

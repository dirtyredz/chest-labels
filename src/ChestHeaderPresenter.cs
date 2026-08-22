using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChestLabels
{
    /// <summary>
    /// Draws a named chest's title into the chest window. Prefers the panel's own header band so
    /// the title reads as part of the window; falls back to a floating overlay plate only if a
    /// game update reshapes the panel and the band can no longer be found.
    ///
    /// Purely a view: it is handed the resolved label + GUID by <see cref="ChestPatches"/> and
    /// owns none of the Harmony wiring. Panel spacing is delegated to
    /// <see cref="ChestPanelGeometry"/>; the diagnostic dumps to <see cref="UiDiagnostics"/>.
    /// </summary>
    internal static class ChestHeaderPresenter
    {
        private const string HeaderObjectName = "ChestLabels_Header";
        /// <summary>Only used by the fallback overlay, so not worth a setting.</summary>
        private const float OverlayFallbackOffsetY = 0f;
        private const string TitleObjectName = "ChestLabels_Title";

        /// <summary>
        /// The chest panel's own title band, confirmed by the runtime hierarchy dump:
        ///
        ///   ChestContainer (410x650)
        ///     Background    410x650
        ///     SlotContainer 340x630          &lt;- no layout group, so children are safe to add
        ///       Header      340x50           &lt;- this band, at the top of the panel
        ///         Ornament  180x50           &lt;- decorative flourish, hidden while titled
        ///         Line      340x4            &lt;- the panel's own divider, kept
        ///       Layout      340x490  [GridLayoutGroup]   &lt;- the item grid, untouched
        ///       Footer      340x50
        /// </summary>
        private const string HeaderBandPath = "SlotContainer/Header";

        /// <summary>
        /// Set when UI injection throws. The header is then left alone for the rest of the
        /// session rather than throwing once per chest open.
        /// </summary>
        internal static bool HeaderDisabledAfterError;

        /// <summary>
        /// Create the header text once and reuse it thereafter. Modelled on the pattern
        /// ExtraTooltip uses: look for an existing child by name first, otherwise build a
        /// GameObject + RectTransform + TextMeshProUGUI and copy the font from a text
        /// element the screen already owns.
        /// </summary>
        public static void ApplyHeader(ChestScreen screen, string label, string chestGuid)
        {
            var container = screen.transform.Find("ChestContainer");
            var band = container == null ? null : container.Find(HeaderBandPath);

            if (band != null)
            {
                HideOverlayPlate(screen);
                ApplyNativeTitle(band, label, chestGuid);
                return;
            }

            // Only reached if a game update reshapes the chest panel. Falls back to a floating
            // plate rather than losing the label entirely - automatic, so it needs no setting.
            ChestLabelsPlugin.Log.LogWarning(
                "Chest panel header band not found; falling back to an overlay label.");

            // Defensive: if a native title had been applied on an earlier open and the panel's
            // shape has since changed, put the flourish back before pasting a plate on top.
            // (Unreachable while band-not-found and band-found are the only two paths, but kept
            // so a future partial-shape fallback restores cleanly.)
            if (band != null)
            {
                ChestPanelGeometry.RestoreOrnament(band);
                HideNativeTitle(band);
            }

            ApplyOverlayPlate(screen, label);
        }

        /// <summary>
        /// Put the label in the panel's own header band, replacing the decorative ornament,
        /// so it reads as part of the chest window rather than an overlay pasted on top.
        /// The divider line below it is left alone - it is what makes the title look native.
        /// </summary>
        private static void ApplyNativeTitle(Transform band, string label, string chestGuid)
        {
            var existing = band.Find(TitleObjectName);

            // Grow the header area and nudge the ornament/divider out of the title's way.
            ChestPanelGeometry.Apply(band);

            TextMeshProUGUI text;

            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                text = existing.GetComponent<TextMeshProUGUI>();
                if (text == null)
                {
                    return;
                }
            }
            else
            {
                var go = new GameObject(TitleObjectName);
                go.transform.SetParent(band, false);
                go.transform.SetAsFirstSibling(); // behind the divider line

                var rect = go.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(-24f, ChestLabelsPlugin.HeaderFontSize.Value + 12f);
                rect.localScale = Vector3.one;

                text = go.AddComponent<TextMeshProUGUI>();
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.raycastTarget = false;
                text.alpha = 1f;
                text.enableAutoSizing = true;
                text.fontSizeMax = ChestLabelsPlugin.HeaderFontSize.Value;
                text.fontSizeMin = 12f;

                // Gelica-Black rather than Bold: the title should carry more weight than the
                // panel's body text. Resolved explicitly rather than copying whichever element
                // happened to be nearest, which was luck of placement.
                GameFonts.Apply(text, preferOutline: false, heavy: true);

                // The same yellow the game uses for item quantities, so the title reads as a
                // heading rather than as body text.
                text.color = GamePalette.CountGold;
            }

            text.text = label ?? string.Empty;

            // An unnamed chest shows no title but must still be nameable, so the text object
            // is kept (hidden) and the pencil button below stays available regardless.
            text.gameObject.SetActive(!string.IsNullOrEmpty(label));

            // Applied every time the screen opens, so both offsets can be tuned in the .cfg
            // and take effect on the next chest without a restart.
            var titleRect = text.rectTransform;
            titleRect.anchoredPosition = new Vector2(0f, ChestLabelsPlugin.TitleOffsetY.Value);
            titleRect.sizeDelta = new Vector2(-24f, ChestLabelsPlugin.HeaderFontSize.Value + 12f);
            text.fontSizeMax = ChestLabelsPlugin.HeaderFontSize.Value;

            if (ChestLabelsPlugin.ShowEditButton.Value)
            {
                TitleEditor.Attach(band, text, chestGuid, label);
            }

            if (ChestLabelsPlugin.LogUiDiagnostics.Value)
            {
                UiDiagnostics.DumpPanelGeometry(band);
            }
        }

        private static void HideNativeTitle(Transform band)
        {
            var title = band.Find(TitleObjectName);
            if (title != null)
            {
                title.gameObject.SetActive(false);
            }
        }

        private static void HideOverlayPlate(ChestScreen screen)
        {
            var plate = ResolveHost(screen).Find(HeaderObjectName);
            if (plate != null)
            {
                plate.gameObject.SetActive(false);
            }
        }

        private static void ApplyOverlayPlate(ChestScreen screen, string label)
        {
            var host = ResolveHost(screen);
            var existing = host.Find(HeaderObjectName);

            if (string.IsNullOrEmpty(label))
            {
                if (existing != null)
                {
                    existing.gameObject.SetActive(false);
                }
                return;
            }

            TextMeshProUGUI text;

            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                text = existing.GetComponentInChildren<TextMeshProUGUI>(true);
                if (text == null)
                {
                    return;
                }

                // Repair a header left invisible by an earlier attempt (no font assigned, or
                // a fully transparent colour) rather than silently reusing a broken one.
                if (text.font == null && TMP_Settings.defaultFontAsset != null)
                {
                    text.font = TMP_Settings.defaultFontAsset;
                }
                if (text.color.a < 0.05f)
                {
                    text.color = GamePalette.NameCream;
                }
                text.alpha = 1f;
            }
            else
            {
                // Plate (background) with the text as its child, so the label reads clearly
                // against the game's busy purple UI instead of floating unbacked.
                var plate = new GameObject(HeaderObjectName);
                plate.transform.SetParent(host, false);
                plate.transform.SetAsLastSibling();

                var plateRect = plate.AddComponent<RectTransform>();
                plateRect.anchorMin = new Vector2(0.5f, 1f);
                plateRect.anchorMax = new Vector2(0.5f, 1f);
                plateRect.pivot = new Vector2(0.5f, 0.5f);
                plateRect.anchoredPosition = new Vector2(0f, OverlayFallbackOffsetY);
                plateRect.sizeDelta = new Vector2(300f, 46f);
                plateRect.localScale = Vector3.one;

                var background = plate.AddComponent<Image>();
                background.color = new Color32(28, 16, 46, 225);
                background.raycastTarget = false;

                var textGo = new GameObject("Text");
                textGo.transform.SetParent(plate.transform, false);

                var textRect = textGo.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(10f, 4f);
                textRect.offsetMax = new Vector2(-10f, -4f);

                text = textGo.AddComponent<TextMeshProUGUI>();
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.raycastTarget = false;
                text.fontSize = ChestLabelsPlugin.HeaderFontSize.Value;

                // Shrink rather than overflow when a label is long.
                text.enableAutoSizing = true;
                text.fontSizeMax = ChestLabelsPlugin.HeaderFontSize.Value;
                text.fontSizeMin = 12f;

                // Borrow only the *font asset* from a text the screen already owns, so the
                // label matches the game's typeface.
                //
                // Deliberately NOT copying colour or material: the donor is found with
                // includeInactive, so it may be a hidden or fully transparent element, and
                // inheriting its colour renders the label invisible with no error. Colour is
                // always set explicitly below.
                GameFonts.Apply(text, preferOutline: true);

                text.color = GamePalette.NameCream;
                text.alpha = 1f;
                text.outlineWidth = 0.2f;
                text.outlineColor = GamePalette.Ink;
            }

            text.text = label;

            if (ChestLabelsPlugin.LogUiDiagnostics.Value)
            {
                UiDiagnostics.DumpHeaderDiagnostics(screen, text);
            }
        }

        /// <summary>
        /// Where to parent the header plate.
        ///
        /// The runtime layout (confirmed by the diagnostics dump) is:
        ///   ChestScreen (1920x1080)
        ///     UIScreenBackground  1920x1080
        ///     InventoryContainer  1250x408   - the player's bag, on the left
        ///     ChestContainer       410x650   - the chest's own panel, on the right
        ///
        /// ChestContainer is the one that belongs to the chest, so the label rides with it.
        /// Falls back progressively so a layout change downgrades rather than breaks.
        /// </summary>
        private static Transform ResolveHost(ChestScreen screen)
        {
            var container = screen.transform.Find("ChestContainer");
            if (container != null)
            {
                return container;
            }

            if (screen.ChestListWidget != null)
            {
                return screen.ChestListWidget.transform;
            }

            return screen.transform;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace ChestLabels
{
    /// <summary>
    /// Per-chest-panel geometry state. Remembers a panel's original spacing so the header's
    /// tweaks - top padding, band drop, ornament nudge, divider nudge - are always computed
    /// from the captured originals rather than accumulated across repeated chest opens.
    ///
    /// Replaces the five parallel <c>Dictionary&lt;int,Vector2&gt;</c> caches that used to sit
    /// in ChestPatches (one per geometry value). One instance per panel, keyed by the panel
    /// RectTransform's InstanceID; each home is captured on first touch and reused thereafter.
    ///
    /// Panel shape (from the runtime hierarchy dump in research/01-chest-system.md):
    ///   ChestContainer                        &lt;- the panel
    ///     SlotContainer
    ///       Header   (the band)
    ///         Ornament                        &lt;- decorative flourish, nudged up
    ///         Line                            &lt;- the panel's divider, nudged down
    ///       Layout   [GridLayoutGroup]        &lt;- the item grid, inset deepened
    /// </summary>
    internal sealed class ChestPanelGeometry
    {
        private static readonly Dictionary<int, ChestPanelGeometry> byPanel =
            new Dictionary<int, ChestPanelGeometry>();

        private bool panelCaptured;
        private Vector2 panelHome;
        private bool layoutCaptured;
        private Vector2 layoutHome;
        private bool bandCaptured;
        private Vector2 bandHome;
        private bool ornamentCaptured;
        private Vector2 ornamentHome;
        private bool lineCaptured;
        private Vector2 lineHome;

        private static ChestPanelGeometry GetOrCreate(RectTransform panel)
        {
            var key = panel.GetInstanceID();
            if (!byPanel.TryGetValue(key, out var state))
            {
                state = new ChestPanelGeometry();
                byPanel[key] = state;
            }

            return state;
        }

        /// <summary>
        /// Make room for the header on this panel: grow the panel, deepen the item grid's top
        /// inset by the same amount (so the grid keeps its height), drop the band, raise the
        /// ornament, and push the divider line down.
        ///
        /// Each step is captured-once and independently guarded, so a panel whose shape has
        /// partly drifted still gets whatever it can. All offsets are read from config every
        /// time, so they can be tuned in the .cfg and take effect on the next chest open.
        /// </summary>
        public static void Apply(Transform band)
        {
            var slotContainer = band.parent;
            var panel = slotContainer != null ? slotContainer.parent as RectTransform : null;
            if (panel == null)
            {
                // band is resolved from the panel, so this never happens; guard anyway.
                return;
            }

            var state = GetOrCreate(panel);
            var layout = slotContainer.Find("Layout") as RectTransform;

            // --- top padding: grow panel + deepen layout inset + drop band ---
            if (layout != null)
            {
                var padding = ChestLabelsPlugin.PanelTopPadding.Value;

                if (!state.panelCaptured)
                {
                    state.panelHome = panel.sizeDelta;
                    state.panelCaptured = true;
                }

                if (!state.layoutCaptured)
                {
                    state.layoutHome = layout.offsetMax;
                    state.layoutCaptured = true;
                }

                // sizeDelta is height-minus-anchor-span, so adding N adds N of real height
                // whether the panel is anchored stretched or fixed.
                panel.sizeDelta = new Vector2(state.panelHome.x, state.panelHome.y + padding);

                // offsetMax.y is measured downward from the top edge, so subtracting pushes the
                // grid's top edge down by the same amount - the grid keeps its original height.
                layout.offsetMax = new Vector2(state.layoutHome.x, state.layoutHome.y - padding);

                // Growing the panel alone is not enough. Header is anchored to the top edge, so
                // a centred growth carries it upward with the panel and the raised ornament ends
                // up tighter to the top. Dropping the band is what creates room above it.
                if (band is RectTransform bandRect)
                {
                    if (!state.bandCaptured)
                    {
                        state.bandHome = bandRect.anchoredPosition;
                        state.bandCaptured = true;
                    }

                    bandRect.anchoredPosition =
                        new Vector2(state.bandHome.x, state.bandHome.y - ChestLabelsPlugin.HeaderDropY.Value);
                }
            }

            // Ornament and line are nudged regardless of whether the padding step ran, mirroring
            // the original (their nudges were separate calls from the padding pass).
            state.NudgeOrnament(band.Find("Ornament") as RectTransform);
            state.NudgeLine(band.Find("Line") as RectTransform);
        }

        /// <summary>
        /// Restore the ornament to its captured home and re-show it. Used by the overlay
        /// fallback: if the panel's shape ever changes after a native title was applied, put the
        /// flourish back before pasting a floating plate on top. A no-op for a panel that was
        /// never touched (nothing captured).
        /// </summary>
        public static void RestoreOrnament(Transform band)
        {
            var ornament = band.Find("Ornament") as RectTransform;
            if (ornament == null)
            {
                return;
            }

            ornament.gameObject.SetActive(true);

            var slotContainer = band.parent;
            var panel = slotContainer != null ? slotContainer.parent as RectTransform : null;
            if (panel != null
                && byPanel.TryGetValue(panel.GetInstanceID(), out var state)
                && state.ornamentCaptured)
            {
                ornament.anchoredPosition = state.ornamentHome;
            }
        }

        private void NudgeOrnament(RectTransform ornament)
        {
            if (ornament == null)
            {
                return;
            }

            // Keep the flourish and nudge it up, so the title sits in the gap between it and the
            // divider line rather than replacing the game's decoration.
            ornament.gameObject.SetActive(true);

            if (!ornamentCaptured)
            {
                ornamentHome = ornament.anchoredPosition;
                ornamentCaptured = true;
            }

            ornament.anchoredPosition = ornamentHome + new Vector2(0f, ChestLabelsPlugin.OrnamentOffsetY.Value);
        }

        private void NudgeLine(RectTransform line)
        {
            if (line == null)
            {
                return;
            }

            // The divider is anchored to the bottom of the band, so pushing it down opens up the
            // space beneath the title. Raising the flourish alone only helps above it.
            if (!lineCaptured)
            {
                lineHome = line.anchoredPosition;
                lineCaptured = true;
            }

            line.anchoredPosition = lineHome + new Vector2(0f, ChestLabelsPlugin.LineOffsetY.Value);
        }
    }
}

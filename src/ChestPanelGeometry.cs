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
    /// in ChestPatches (one per geometry value) with a single per-panel state object, keyed by
    /// the panel RectTransform's InstanceID. Each home is captured on first touch and reused
    /// thereafter.
    ///
    /// This assumes a panel's child transforms (Header / Ornament / Line / Layout) are stable
    /// across chest opens - true here, because the game reuses one chest panel instance and
    /// only shows/hides it. If a game update ever rebuilt those children under a surviving
    /// panel, a new child would inherit the old child's captured home; that trade is accepted
    /// in exchange for one state object instead of five identity-keyed caches.
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

        private Vector2? panelHome;
        private Vector2? layoutHome;
        private Vector2? bandHome;
        private Vector2? ornamentHome;
        private Vector2? lineHome;

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

        /// <summary>The panel is the band's grandparent: Header -&gt; SlotContainer -&gt; ChestContainer.</summary>
        private static RectTransform ResolvePanel(Transform band)
        {
            var slotContainer = band.parent;
            return slotContainer != null ? slotContainer.parent as RectTransform : null;
        }

        /// <summary>
        /// Make room for the header on this panel: grow the panel, deepen the item grid's top
        /// inset by the same amount (so the grid keeps its height), drop the band, raise the
        /// ornament, and push the divider line down.
        ///
        /// The padding step and the two nudges are guarded independently and best-effort, so a
        /// panel whose shape has partly drifted (e.g. a game update renames Layout) still gets
        /// whatever it can - the ornament and line are nudged even when the padding step can't
        /// run. All offsets are read from config every time, so they can be tuned in the .cfg
        /// and take effect on the next chest open.
        /// </summary>
        public static void Apply(Transform band)
        {
            var panel = ResolvePanel(band);
            if (panel == null)
            {
                // band is resolved from the panel, so this never happens; guard anyway.
                return;
            }

            var state = GetOrCreate(panel);
            var layout = band.parent.Find("Layout") as RectTransform;

            // --- top padding: grow panel + deepen layout inset + drop band ---
            if (layout != null)
            {
                var padding = ChestLabelsPlugin.PanelTopPadding.Value;

                // sizeDelta is height-minus-anchor-span, so adding N adds N of real height
                // whether the panel is anchored stretched or fixed.
                var panelBase = state.Capture(ref state.panelHome, panel.sizeDelta);
                panel.sizeDelta = new Vector2(panelBase.x, panelBase.y + padding);

                // offsetMax.y is measured downward from the top edge, so subtracting pushes the
                // grid's top edge down by the same amount - the grid keeps its original height.
                var layoutBase = state.Capture(ref state.layoutHome, layout.offsetMax);
                layout.offsetMax = new Vector2(layoutBase.x, layoutBase.y - padding);

                // Growing the panel alone is not enough. Header is anchored to the top edge, so
                // a centred growth carries it upward with the panel and the raised ornament ends
                // up tighter to the top. Dropping the band is what creates room above it.
                if (band is RectTransform bandRect)
                {
                    var bandBase = state.Capture(ref state.bandHome, bandRect.anchoredPosition);
                    bandRect.anchoredPosition =
                        new Vector2(bandBase.x, bandBase.y - ChestLabelsPlugin.HeaderDropY.Value);
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

            var panel = ResolvePanel(band);
            if (panel != null
                && byPanel.TryGetValue(panel.GetInstanceID(), out var state)
                && state.ornamentHome.HasValue)
            {
                ornament.anchoredPosition = state.ornamentHome.Value;
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

            var home = Capture(ref ornamentHome, ornament.anchoredPosition);
            ornament.anchoredPosition = home + new Vector2(0f, ChestLabelsPlugin.OrnamentOffsetY.Value);
        }

        private void NudgeLine(RectTransform line)
        {
            if (line == null)
            {
                return;
            }

            // The divider is anchored to the bottom of the band, so pushing it down opens up the
            // space beneath the title. Raising the flourish alone only helps above it.
            var home = Capture(ref lineHome, line.anchoredPosition);
            line.anchoredPosition = home + new Vector2(0f, ChestLabelsPlugin.LineOffsetY.Value);
        }

        /// <summary>Capture <paramref name="current"/> the first time only; return the captured home.</summary>
        private Vector2 Capture(ref Vector2? home, Vector2 current)
        {
            if (home == null)
            {
                home = current;
            }

            return home.Value;
        }
    }
}

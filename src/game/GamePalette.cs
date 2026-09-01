using UnityEngine;

namespace ChestLabels
{
    /// <summary>
    /// The game's colours, in one place, so no element invents its own.
    ///
    /// See <c>10-visual-integration.md</c> at the repo root for why this matters.
    ///
    /// Values are read off the game's own UI. Where a value is estimated from a screenshot
    /// rather than sampled from the shipped asset it says so — those are the ones to verify
    /// if something looks slightly off.
    /// </summary>
    internal static class GamePalette
    {
        /// <summary>
        /// Label text. The pale cream used on the game's world nameplates — noticeably lighter
        /// than the gold of inventory item counts, and the right colour for a name.
        /// Estimated from a screenshot of the "Chester" nameplate.
        /// </summary>
        internal static readonly Color32 NameCream = new Color32(0xF8, 0xF0, 0xC6, 0xFF);

        /// <summary>Warm gold, as used for item quantities in inventory panels.</summary>
        internal static readonly Color32 CountGold = new Color32(0xF7, 0xD9, 0x94, 0xFF);

        /// <summary>Mid royal purple of the world nameplate banner. Estimated.</summary>
        internal static readonly Color32 NameplatePurple = new Color32(0x5A, 0x2D, 0x91, 0xFF);

        /// <summary>Darker purple rim beneath the nameplate banner. Estimated.</summary>
        internal static readonly Color32 NameplateRim = new Color32(0x3B, 0x1B, 0x63, 0xFF);

        /// <summary>Near-black plum of the inventory and chest windows.</summary>
        internal static readonly Color32 PanelPlum = new Color32(0x1B, 0x0F, 0x2E, 0xFF);

        /// <summary>Dark ink used for outlines throughout the game's art.</summary>
        internal static readonly Color32 Ink = new Color32(0x2A, 0x1B, 0x3D, 0xFF);
    }
}

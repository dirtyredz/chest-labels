using System;
using Chicken.UI;
using UnityEngine;

namespace ChestLabels
{
    /// <summary>
    /// Answers "which chest is the player pointing at?" for the hover label, and the related
    /// "should the label / interaction arrow show?" queries.
    ///
    /// Prefers the game's own interaction target (PlayerCursorInteractionScreen.showingSource,
    /// read by reflection) so the label and the game's interaction arrow agree exactly, and
    /// falls back to a mouse raycast when that target is empty or the reflection is unavailable.
    /// Split out of <see cref="HoverLabel"/> so detection is separable from rendering.
    /// </summary>
    internal static class ChestInteractionSource
    {
        private const float RaycastDistance = 200f;

        private static System.Reflection.FieldInfo showingSourceField;
        private static System.Reflection.FieldInfo sourceContextField;
        private static bool interactionLookupUnavailable;
        private static bool loggedFirstHit;

        /// <summary>True when the game's interaction target is readable, so polling is cheap.</summary>
        internal static bool UsingInteractionSource => !interactionLookupUnavailable;

        /// <summary>
        /// Whether the hover label should keep quiet.
        ///
        /// Deliberately NOT "any screen is showing": EnergyScreen, ManaScreen and the
        /// interaction prompts are permanently in UIScreen.ShowStack during normal play, so
        /// that test suppresses the label forever. Only genuinely modal things count.
        ///
        /// A positive gate rather than a blocklist: PlayerCursorInteractionScreen is up exactly
        /// when the player can point at the world, and absent in every case the label should
        /// stay hidden (chest open, pause, cutscene, menus).
        /// </summary>
        internal static bool ShouldStandDown()
        {
            var cursorScreen = UIScreen<PlayerCursorInteractionScreen>.Instance;
            return cursorScreen == null || !cursorScreen.IsShowing;
        }

        /// <summary>
        /// The chest under the player's pointer: the game's interaction target if readable,
        /// otherwise a mouse raycast. Null if none.
        /// </summary>
        internal static Chest FindChest(Camera camera) =>
            FindChestFromInteractionTarget() ?? FindChestUnderMouse(camera);

        /// <summary>
        /// Whether the interaction arrow should stand down this frame.
        ///
        /// Computed from the game's current interaction target rather than from whether the
        /// label happens to be on screen yet. Update order between the label component and the
        /// interaction screen is undefined, and the label polls on an interval - so observing
        /// the label's own state left a frame or two where both the arrow and the label were
        /// visible. Deriving the answer from the same data, in the caller's frame, removes that
        /// window. Cheap enough to call every frame: two field reads and a dictionary lookup.
        /// </summary>
        internal static bool ShouldSuppressArrow()
        {
            if (!ChestLabelsPlugin.ShowHoverLabel.Value)
            {
                return false;
            }

            var chest = FindChestFromInteractionTarget();
            if (chest == null)
            {
                return false;
            }

            if (!ChestLabelsPlugin.EnsureStoreLoaded())
            {
                return false;
            }

            var guid = GetGuid(chest);
            return guid != null && !string.IsNullOrEmpty(ChestLabelsPlugin.Store?.Get(guid));
        }

        internal static string GetGuid(Chest chest)
        {
            var persistence = chest?.GridObjectPersistence;
            if (persistence == null)
            {
                return null;
            }

            var guid = persistence.Guid.ToString();
            return string.IsNullOrWhiteSpace(guid) ? null : guid.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// The chest the game's own cursor-interaction screen is currently showing for - the
        /// same thing the interaction arrow is attached to.
        ///
        /// Using this instead of a private raycast means the label and the arrow appear and
        /// disappear together, at the game's interaction range rather than at whatever a
        /// collider hit happens to give. Returns null if unavailable, and the caller falls back
        /// to the raycast.
        /// </summary>
        private static Chest FindChestFromInteractionTarget()
        {
            if (interactionLookupUnavailable)
            {
                return null;
            }

            try
            {
                var screen = UIScreen<PlayerCursorInteractionScreen>.Instance;
                if (screen == null)
                {
                    return null;
                }

                if (showingSourceField == null)
                {
                    // Declared protected on the generic base BaseInteractionScreen<T>.
                    showingSourceField = HarmonyLib.AccessTools.Field(
                        typeof(PlayerCursorInteractionScreen), "showingSource");

                    if (showingSourceField == null)
                    {
                        interactionLookupUnavailable = true;
                        ChestLabelsPlugin.Log.LogWarning(
                            "Interaction source unavailable; falling back to mouse raycast.");
                        return null;
                    }
                }

                var source = showingSourceField.GetValue(screen);
                if (source == null)
                {
                    return null;
                }

                if (sourceContextField == null)
                {
                    sourceContextField = HarmonyLib.AccessTools.Field(source.GetType(), "Context");
                    if (sourceContextField == null)
                    {
                        interactionLookupUnavailable = true;
                        return null;
                    }
                }

                // Context is the interactable itself for chests; walk up for safety in case a
                // child collider is registered instead.
                switch (sourceContextField.GetValue(source))
                {
                    case Chest chest:
                        return chest;
                    case Component component:
                        return component.GetComponentInParent<Chest>();
                    default:
                        return null;
                }
            }
            catch (Exception e)
            {
                interactionLookupUnavailable = true;
                ChestLabelsPlugin.Log.LogWarning(
                    $"Interaction source lookup failed, using mouse raycast instead: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Interaction colliders are frequently triggers, which a plain raycast skips - hence
        /// QueryTriggerInteraction.Collide and RaycastAll rather than the first hit only.
        /// </summary>
        private static Chest FindChestUnderMouse(Camera camera)
        {
            if (camera == null)
            {
                return null;
            }

            // Fully qualified: an `Input` type in one of the game's own namespaces would
            // otherwise shadow UnityEngine's.
            var ray = camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            var hits = Physics.RaycastAll(ray, RaycastDistance, ~0, QueryTriggerInteraction.Collide);

            Chest best = null;
            var bestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                var chest = hit.collider.GetComponentInParent<Chest>();
                if (chest != null && hit.distance < bestDistance)
                {
                    best = chest;
                    bestDistance = hit.distance;
                }
            }

            // One-off confirmation that the raycast reaches world geometry at all. If this
            // never appears, the ray is missing everything and the layer mask or collider
            // setup is the thing to look at next.
            if (!loggedFirstHit && hits.Length > 0 && ChestLabelsPlugin.VerboseLogging.Value)
            {
                loggedFirstHit = true;
                ChestLabelsPlugin.Log.LogInfo(
                    $"Hover raycast working: {hits.Length} collider(s) under cursor, " +
                    $"first = '{hits[0].collider.name}', chest found = {best != null}");
            }

            return best;
        }
    }
}

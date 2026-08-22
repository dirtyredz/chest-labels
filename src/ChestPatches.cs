using System;
using HarmonyLib;
using UnityEngine;

namespace ChestLabels
{
    /// <summary>
    /// Harmony patches against the storage-chest flow.
    ///
    /// Hook points were established by decompiling Vampire.Runtime.dll - see
    /// research/01-chest-system.md. Every patch body is wrapped defensively: a mod that
    /// throws inside a UI callback can wedge the screen, and no label is worth that.
    ///
    /// This type is deliberately kept to the patch methods and their small chest-lookup
    /// helpers. Drawing the header lives in <see cref="ChestHeaderPresenter"/>, panel spacing
    /// in <see cref="ChestPanelGeometry"/>, and the diagnostic dumps in <see cref="UiDiagnostics"/>.
    /// </summary>
    internal static class ChestPatches
    {
        // --- foreign-patch guard ------------------------------------------------------------
        // Another mod (e.g. an outdated ExtraTooltip) can Harmony-patch NameplateScreen.Show and
        // throw from its own postfix when a game update renames a type it referenced. The game's
        // original Show has already drawn the nameplate by the time postfixes run, so a finalizer
        // can swallow that foreign failure and our label still shows - no need to fall back to a
        // self-drawn plate. Only the stale-reference exception kinds are suppressed, once logged.
        private static bool warnedForeignNameplate;

        [HarmonyPatch(typeof(NameplateScreen), "Show", new[] { typeof(RectTransform), typeof(INameplateData) })]
        [HarmonyFinalizer]
        private static void NameplateScreen_Show_Finalizer(ref Exception __exception)
        {
            if (__exception == null)
            {
                return;
            }

            if (__exception is TypeLoadException
                || __exception is TypeInitializationException
                || __exception is MissingMemberException)
            {
                if (!warnedForeignNameplate)
                {
                    warnedForeignNameplate = true;
                    ChestLabelsPlugin.Log.LogWarning(
                        "Another mod's NameplateScreen.Show patch threw (" + __exception.GetType().Name +
                        "); suppressing it so the game nameplate still works. This is usually an " +
                        "outdated tooltip mod after a game update.");
                }

                // Base nameplate already rendered before postfixes ran - safe to drop this.
                __exception = null;
            }
        }

        /// <summary>
        /// Chest opened. PlayerStorageChestState.OnActivate assigns its private `chest`
        /// field before we run, so a Postfix sees it populated.
        /// </summary>
        [HarmonyPatch(typeof(PlayerStorageChestState), "OnActivate")]
        [HarmonyPostfix]
        private static void PlayerStorageChestState_OnActivate(PlayerStorageChestState __instance, Chest ___chest)
        {
            try
            {
                var chestGuid = GetChestGuid(___chest);
                if (chestGuid == null)
                {
                    return;
                }

                if (!ChestLabelsPlugin.EnsureStoreLoaded())
                {
                    return;
                }

                var label = ChestLabelsPlugin.Store.Get(chestGuid);

                if (label != null)
                {
                    ChestLabelsPlugin.Log.LogInfo($"Opened chest {chestGuid} - \"{label}\"");
                }
                else if (ChestLabelsPlugin.VerboseLogging.Value)
                {
                    // Until there is an in-game rename field, this line is how you name a
                    // chest: copy it into the sidecar file and press the reload key.
                    ChestLabelsPlugin.Log.LogInfo(
                        $"Opened chest {chestGuid} (no label). To name it, add this line to " +
                        $"{ChestLabelsPlugin.Store.CurrentPath} :");
                    ChestLabelsPlugin.Log.LogInfo($"    \"{chestGuid}\": \"My label\"");
                }

                if (ChestLabelsPlugin.VerboseLogging.Value)
                {
                    LogContents(___chest);
                }
            }
            catch (Exception e)
            {
                ChestLabelsPlugin.Log.LogError($"OnActivate patch failed: {e}");
            }
        }

        /// <summary>
        /// Freeze the chest screen's own hotkeys while the rename field has focus.
        ///
        /// OnActiveUpdate reads them through Rewired (Input.Player.GetButtonDownConsumed),
        /// which does not care that Unity's EventSystem has given keyboard focus to a text
        /// field - so without this, typing a name containing "x" also reorders the chest.
        /// </summary>
        [HarmonyPatch(typeof(BasePlayerChestState), "OnActiveUpdate")]
        [HarmonyPrefix]
        private static bool BasePlayerChestState_OnActiveUpdate() => !TitleEditor.IsEditing;

        /// <summary>
        /// Show a named chest's label in the game's own interaction banner — the purple plate
        /// that reads "Chester" when you point at him.
        ///
        /// BasePlayerState builds that label from Interactable.InteractionText:
        ///     Text = (flag ? target2.InteractionText : string.Empty)
        ///
        /// So overriding it for named chests means the game draws the name itself: its banner,
        /// its font and colour, its position, and — the part that matters most — the exact same
        /// trigger as the interaction arrow. No second detection to keep in sync, no window
        /// where one is visible and the other is not.
        ///
        /// This is why the custom hover plate exists only as a fallback now. An earlier attempt
        /// used NameplateScreen, which turned out to be the orange speech bubble rather than
        /// this banner.
        /// </summary>
        [HarmonyPatch(typeof(Interactable), nameof(Interactable.InteractionText), MethodType.Getter)]
        [HarmonyPostfix]
        private static void Interactable_InteractionText(Interactable __instance, ref string __result)
        {
            if (!ChestLabelsPlugin.UseGameInteractionLabel.Value)
            {
                return;
            }

            try
            {
                if (!(__instance is Chest chest))
                {
                    return;
                }

                var chestGuid = GetChestGuid(chest);
                if (chestGuid == null || !ChestLabelsPlugin.EnsureStoreLoaded())
                {
                    return;
                }

                var label = ChestLabelsPlugin.Store.Get(chestGuid);
                if (!string.IsNullOrEmpty(label))
                {
                    __result = label;
                }
            }
            catch (Exception e)
            {
                ChestLabelsPlugin.Log.LogError($"Interaction label patch failed; disabling it. {e}");
                ChestLabelsPlugin.UseGameInteractionLabel.Value = false;
            }
        }

        private static System.Reflection.FieldInfo arrowWidgetField;
        private static bool arrowHiddenByUs;

        /// <summary>
        /// Hide the vanilla interaction chevron while a named chest's label is showing — the
        /// label already says "there is something here", so the arrow is redundant clutter
        /// sitting right where the label wants to be.
        ///
        /// Patches PlayerCursorInteractionScreen.Update rather than hiding the screen itself:
        /// the screen is what HoverLabel uses to decide the player can point at the world, so
        /// hiding it would make the label hide, which would bring the screen back — a flicker
        /// loop. Only the arrow graphic is touched, and only while we are showing something.
        ///
        /// Running as a postfix on the screen's own Update means the game cannot re-show the
        /// arrow behind us on a later frame.
        /// </summary>
        [HarmonyPatch(typeof(PlayerCursorInteractionScreen), "Update")]
        [HarmonyPostfix]
        private static void PlayerCursorInteractionScreen_Update(PlayerCursorInteractionScreen __instance)
        {
            try
            {
                // Derived from the game's current interaction target, not from whether our
                // label has rendered yet — the latter lagged by a frame or two, leaving a
                // window where both the arrow and the label were on screen.
                var shouldHide = ChestLabelsPlugin.HideArrowWhenNamed.Value && HoverLabel.ShouldSuppressArrow();

                if (!shouldHide && !arrowHiddenByUs)
                {
                    return; // nothing to do, and nothing of ours to restore
                }

                if (arrowWidgetField == null)
                {
                    // Declared on the generic base; AccessTools walks base types for it.
                    arrowWidgetField = AccessTools.Field(typeof(PlayerCursorInteractionScreen), "arrowWidget");
                    if (arrowWidgetField == null)
                    {
                        ChestLabelsPlugin.Log.LogWarning(
                            "Could not find the interaction arrow; leaving it alone.");
                        ChestLabelsPlugin.HideArrowWhenNamed.Value = false;
                        return;
                    }
                }

                if (!(arrowWidgetField.GetValue(__instance) is Component arrow) || arrow == null)
                {
                    return;
                }

                if (arrow.gameObject.activeSelf == shouldHide)
                {
                    arrow.gameObject.SetActive(!shouldHide);
                    arrowHiddenByUs = shouldHide;
                }
            }
            catch (Exception e)
            {
                ChestLabelsPlugin.Log.LogError($"Arrow hiding failed; disabling it. {e}");
                ChestLabelsPlugin.HideArrowWhenNamed.Value = false;
            }
        }

        /// <summary>Set once the CustomName patch throws, so it stands down for the session.</summary>
        private static bool screenReaderNameDisabledAfterError;

        /// <summary>
        /// Expose a named chest's label through the game's own <c>CustomName</c> field so screen
        /// readers - MoonlightAccess in particular - announce it. MoonlightAccess names a placed
        /// object from <c>GridObjectPersistence.CustomName</c> and falls back to the asset name
        /// ("Storage Crate") when it is empty, which is why an unnamed-to-the-game chest reads as
        /// its type rather than the player's label.
        ///
        /// This is a read-time overlay only. It decorates the property's return value; it never
        /// writes <c>ItemEntry.CustomName</c>, so:
        ///   - the save is untouched (see research/02-save-format.md for why the field is left
        ///     unwritten deliberately), and
        ///   - item stacking/identity is unaffected, because the compare mask reads the raw
        ///     struct field, not this property.
        ///
        /// A real game-set CustomName is never overridden - the label only fills the otherwise
        /// dormant field. The sidecar key is the grid object's GUID, so a lookup hit is itself
        /// proof the object is one of our labelled chests; no type check is needed.
        /// </summary>
        [HarmonyPatch(typeof(GridObjectPersistence), nameof(GridObjectPersistence.CustomName), MethodType.Getter)]
        [HarmonyPostfix]
        private static void GridObjectPersistence_CustomName(GridObjectPersistence __instance, ref string __result)
        {
            if (screenReaderNameDisabledAfterError)
            {
                return;
            }

            try
            {
                var mode = ChestLabelsPlugin.ScreenReaderName.Value;
                if (mode == ScreenReaderNameMode.Off)
                {
                    return;
                }

                // Only ever fill the empty field; never clobber a name the game itself set.
                if (!string.IsNullOrEmpty(__result) || __instance == null)
                {
                    return;
                }

                if (!ChestLabelsPlugin.EnsureStoreLoaded())
                {
                    return;
                }

                var guid = __instance.Guid.ToString();
                if (string.IsNullOrWhiteSpace(guid))
                {
                    return;
                }

                // Store.Get normalizes the key itself, so the raw GUID string is fine here.
                var label = ChestLabelsPlugin.Store?.Get(guid);
                if (string.IsNullOrEmpty(label))
                {
                    return;
                }

                if (mode == ScreenReaderNameMode.TypeAndLabel)
                {
                    var itemAsset = __instance.ItemAsset;
                    var type = itemAsset == null ? null : itemAsset.Name;
                    __result = string.IsNullOrEmpty(type) ? label : $"{type} named {label}";
                }
                else
                {
                    __result = label;
                }
            }
            catch (Exception e)
            {
                // A getter that throws would fire on every read; stand down for the session.
                screenReaderNameDisabledAfterError = true;
                ChestLabelsPlugin.Log.LogError(
                    $"Screen-reader name patch failed; disabling it for this session. {e}");
            }
        }

        /// <summary>
        /// Chest destroyed. Prefix, because Chest.Delete nulls GridObjectPersistence and we
        /// need the GUID to prune the label before it goes.
        /// </summary>
        [HarmonyPatch(typeof(Chest), "Delete")]
        [HarmonyPrefix]
        private static void Chest_Delete(Chest __instance)
        {
            try
            {
                var chestGuid = GetChestGuid(__instance);
                if (chestGuid == null || ChestLabelsPlugin.Store?.SaveGuid == null)
                {
                    return;
                }

                if (ChestLabelsPlugin.Store.Remove(chestGuid))
                {
                    ChestLabelsPlugin.Store.Save();
                    ChestLabelsPlugin.Log.LogInfo($"Chest {chestGuid} removed - label pruned.");
                }
            }
            catch (Exception e)
            {
                ChestLabelsPlugin.Log.LogError($"Delete patch failed: {e}");
            }
        }

        /// <summary>
        /// Chest screen shown. ChestScreen holds the chest in a private `chest` field.
        /// </summary>
        [HarmonyPatch(typeof(ChestScreen), "OnShow")]
        [HarmonyPostfix]
        private static void ChestScreen_OnShow(ChestScreen __instance, Chest ___chest)
        {
            if (!ChestLabelsPlugin.ShowHeader.Value || ChestHeaderPresenter.HeaderDisabledAfterError)
            {
                return;
            }

            try
            {
                // ChestScreen.OnShow runs *inside* PlayerStorageChestState.OnActivate, so it
                // fires before that method's postfix. The store must therefore be loaded here
                // too - relying on OnActivate meant the first chest opened in a session found
                // an empty store and silently rendered no header.
                ChestLabelsPlugin.EnsureStoreLoaded();

                var chestGuid = GetChestGuid(___chest);
                var label = chestGuid == null ? null : ChestLabelsPlugin.Store?.Get(chestGuid);

                if (string.IsNullOrEmpty(label) && ChestLabelsPlugin.VerboseLogging.Value)
                {
                    ChestLabelsPlugin.Log.LogInfo(
                        $"No header drawn: chest {chestGuid ?? "<unknown>"} has no label.");
                }

                ChestHeaderPresenter.ApplyHeader(__instance, label, chestGuid);
            }
            catch (Exception e)
            {
                // Stand down for the session rather than throwing on every chest open.
                ChestHeaderPresenter.HeaderDisabledAfterError = true;
                ChestLabelsPlugin.Log.LogError(
                    $"Header injection failed; disabling the header for this session. " +
                    $"Labels still work and are still logged. Details: {e}");
            }
        }

        private static void LogContents(Chest chest)
        {
            var inventory = chest?.Inventory;
            if (inventory?.Slots == null)
            {
                return;
            }

            var used = 0;
            foreach (var slot in inventory.Slots)
            {
                if (slot != null && !slot.IsEmpty)
                {
                    used++;
                }
            }

            ChestLabelsPlugin.Log.LogInfo($"    contents: {used} slot(s) in use");
        }

        private static string GetChestGuid(Chest chest)
        {
            var persistence = chest?.GridObjectPersistence;
            if (persistence == null)
            {
                return null;
            }

            var guid = persistence.Guid.ToString();
            return string.IsNullOrWhiteSpace(guid) ? null : guid.Trim().ToLowerInvariant();
        }
    }
}

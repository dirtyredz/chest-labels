using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChestLabels
{
    /// <summary>
    /// Harmony patches against the storage-chest flow.
    ///
    /// Hook points were established by decompiling Vampire.Runtime.dll - see
    /// research/01-chest-system.md. Every patch body is wrapped defensively: a mod that
    /// throws inside a UI callback can wedge the screen, and no label is worth that.
    /// </summary>
    internal static class ChestPatches
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
            if (!ChestLabelsPlugin.ShowHeader.Value || ChestLabelsPlugin.HeaderDisabledAfterError)
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

                ApplyHeader(__instance, label, chestGuid);
            }
            catch (Exception e)
            {
                // Stand down for the session rather than throwing on every chest open.
                ChestLabelsPlugin.HeaderDisabledAfterError = true;
                ChestLabelsPlugin.Log.LogError(
                    $"Header injection failed; disabling the header for this session. " +
                    $"Labels still work and are still logged. Details: {e}");
            }
        }

        /// <summary>
        /// Create the header text once and reuse it thereafter. Modelled on the pattern
        /// ExtraTooltip uses: look for an existing child by name first, otherwise build a
        /// GameObject + RectTransform + TextMeshProUGUI and copy the font from a text
        /// element the screen already owns.
        /// </summary>
        private static void ApplyHeader(ChestScreen screen, string label, string chestGuid)
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

            // Fall back to the floating plate if the panel's shape ever changes.
            if (band != null)
            {
                RestoreOrnament(band);
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
            var ornament = band.Find("Ornament") as RectTransform;

            ApplyTopPadding(band);

            // Keep the flourish and nudge it up, so the title sits in the gap between it and
            // the divider line rather than replacing the game's decoration.
            NudgeOrnament(ornament);

            // The divider is anchored to the bottom of the band, so pushing it down opens up
            // the space beneath the title. Raising the flourish alone only helps above it.
            NudgeLine(band.Find("Line") as RectTransform);

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
                // Dump from the panel root, not just the band: resizing the header area needs
                // the anchors of SlotContainer / Header / Layout / Footer, and a band-only
                // dump does not show them.
                var panel = band.parent != null ? band.parent.parent : band;
                ChestLabelsPlugin.Log.LogInfo("--- chest panel geometry ---");
                DumpTree(panel != null ? panel : band, "    ");
                ChestLabelsPlugin.Log.LogInfo("--- end geometry ---");
            }
        }

        /// <summary>
        /// Give the header area more breathing room without shrinking the item grid.
        ///
        /// The measured layout is:
        ///   SlotContainer  340x630  anchors (0,0)-(1,1)   stretches with the panel
        ///     Header       340x50   anchors (0,1)-(1,1)   pinned 45 below the top
        ///     Layout       340x490  anchors (0,0)-(1,1)   stretched, inset 70 top / 70 bottom
        ///
        /// Because Layout is stretched with insets rather than a fixed size, growing the panel
        /// by N *and* deepening Layout's top inset by N leaves the grid exactly its original
        /// height while the header gains N of space. Footer is anchored to the bottom, so it
        /// is unaffected.
        /// </summary>
        private static void ApplyTopPadding(Transform band)
        {
            var padding = ChestLabelsPlugin.PanelTopPadding.Value;

            var slotContainer = band.parent;
            var panel = slotContainer != null ? slotContainer.parent as RectTransform : null;
            var layout = slotContainer != null ? slotContainer.Find("Layout") as RectTransform : null;

            if (panel == null || layout == null)
            {
                return;
            }

            // Always computed from the captured originals, never accumulated, or repeated
            // chest opens would keep stretching the window.
            var panelKey = panel.GetInstanceID();
            if (!panelHeights.TryGetValue(panelKey, out var panelHome))
            {
                panelHome = panel.sizeDelta;
                panelHeights[panelKey] = panelHome;
            }

            var layoutKey = layout.GetInstanceID();
            if (!layoutInsets.TryGetValue(layoutKey, out var layoutHome))
            {
                layoutHome = layout.offsetMax;
                layoutInsets[layoutKey] = layoutHome;
            }

            // sizeDelta is height-minus-anchor-span, so adding N adds N of real height whether
            // the panel is anchored stretched or fixed.
            panel.sizeDelta = new Vector2(panelHome.x, panelHome.y + padding);

            // offsetMax.y is measured downward from the top edge, so subtracting pushes the
            // grid's top edge down by the same amount.
            layout.offsetMax = new Vector2(layoutHome.x, layoutHome.y - padding);

            // Growing the panel alone is not enough. Header is anchored to the top edge, so a
            // centred growth carries it upward with the panel and the raised ornament ends up
            // tighter to the top, not looser. Dropping the band is what actually creates room
            // above the decoration.
            var bandRect = band as RectTransform;
            if (bandRect == null)
            {
                return;
            }

            var bandKey = bandRect.GetInstanceID();
            if (!headerHomes.TryGetValue(bandKey, out var bandHome))
            {
                bandHome = bandRect.anchoredPosition;
                headerHomes[bandKey] = bandHome;
            }

            bandRect.anchoredPosition =
                new Vector2(bandHome.x, bandHome.y - ChestLabelsPlugin.HeaderDropY.Value);
        }

        private static readonly System.Collections.Generic.Dictionary<int, Vector2> headerHomes =
            new System.Collections.Generic.Dictionary<int, Vector2>();

        private static readonly System.Collections.Generic.Dictionary<int, Vector2> panelHeights =
            new System.Collections.Generic.Dictionary<int, Vector2>();

        private static readonly System.Collections.Generic.Dictionary<int, Vector2> layoutInsets =
            new System.Collections.Generic.Dictionary<int, Vector2>();

        /// <summary>
        /// Original ornament positions, captured before the first nudge.
        ///
        /// Without this the offset would compound every time a chest is opened, walking the
        /// flourish off the top of the panel.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<int, Vector2> ornamentHomes =
            new System.Collections.Generic.Dictionary<int, Vector2>();

        private static void NudgeOrnament(RectTransform ornament)
        {
            if (ornament == null)
            {
                return;
            }

            ornament.gameObject.SetActive(true);

            var key = ornament.GetInstanceID();
            if (!ornamentHomes.TryGetValue(key, out var home))
            {
                home = ornament.anchoredPosition;
                ornamentHomes[key] = home;
            }

            ornament.anchoredPosition = home + new Vector2(0f, ChestLabelsPlugin.OrnamentOffsetY.Value);
        }

        private static readonly System.Collections.Generic.Dictionary<int, Vector2> lineHomes =
            new System.Collections.Generic.Dictionary<int, Vector2>();

        /// <summary>
        /// Push the header band's divider line down, opening space between the title and the
        /// line. Computed from the cached original so repeated opens never accumulate.
        /// </summary>
        private static void NudgeLine(RectTransform line)
        {
            if (line == null)
            {
                return;
            }

            var key = line.GetInstanceID();
            if (!lineHomes.TryGetValue(key, out var home))
            {
                home = line.anchoredPosition;
                lineHomes[key] = home;
            }

            line.anchoredPosition = home + new Vector2(0f, ChestLabelsPlugin.LineOffsetY.Value);
        }

        private static void RestoreOrnamentPosition(RectTransform ornament)
        {
            if (ornament == null)
            {
                return;
            }

            ornament.gameObject.SetActive(true);

            if (ornamentHomes.TryGetValue(ornament.GetInstanceID(), out var home))
            {
                ornament.anchoredPosition = home;
            }
        }

        private static void RestoreOrnament(Transform band)
        {
            var title = band.Find(TitleObjectName);
            if (title != null)
            {
                title.gameObject.SetActive(false);
            }

            RestoreOrnamentPosition(band.Find("Ornament") as RectTransform);
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
                DumpHeaderDiagnostics(screen, text);
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

        /// <summary>
        /// Pick a visible text element to borrow the font from. Prefers an active one, since
        /// inactive elements are more likely to be leftovers with odd settings.
        /// </summary>
        private static TextMeshProUGUI FindFontDonor(ChestScreen screen, TextMeshProUGUI exclude) =>
            FindFontDonorIn(screen.transform, exclude);

        private static TextMeshProUGUI FindFontDonorIn(Transform root, TextMeshProUGUI exclude)
        {
            TextMeshProUGUI fallback = null;

            foreach (var candidate in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (candidate == null || candidate == exclude || candidate.font == null)
                {
                    continue;
                }

                if (candidate.gameObject.activeInHierarchy)
                {
                    return candidate;
                }

                fallback = fallback ?? candidate;
            }

            return fallback;
        }

        /// <summary>
        /// One-shot dump of everything that decides whether the header is visible. Costs a
        /// little log noise and saves a lot of guessing; turn off once positioning is right.
        /// </summary>
        private static void DumpHeaderDiagnostics(ChestScreen screen, TextMeshProUGUI text)
        {
            try
            {
                var log = ChestLabelsPlugin.Log;
                var screenRect = screen.transform as RectTransform;
                var rect = text.rectTransform;
                var canvas = text.GetComponentInParent<Canvas>();

                log.LogInfo("--- header diagnostics ---");
                log.LogInfo($"  screen root      : {screen.gameObject.name} " +
                            $"active={screen.gameObject.activeInHierarchy} " +
                            $"scale={screen.transform.lossyScale}");
                log.LogInfo($"  screen rect size : {(screenRect == null ? "NOT a RectTransform" : screenRect.rect.size.ToString())}");
                log.LogInfo($"  canvas           : {(canvas == null ? "NULL - text cannot render" : canvas.name + " enabled=" + canvas.enabled)}");
                log.LogInfo($"  font             : {(text.font == null ? "NULL - text is invisible" : text.font.name)}");
                log.LogInfo($"  colour/alpha     : {text.color} alpha={text.alpha}");
                log.LogInfo($"  fontSize         : {text.fontSize}");
                log.LogInfo($"  anchoredPosition : {rect.anchoredPosition}  sizeDelta={rect.sizeDelta}");
                log.LogInfo($"  world corners    : {DescribeCorners(rect)}");

                var group = screen.GetComponentInChildren<CanvasGroup>(true);
                if (group != null)
                {
                    log.LogInfo($"  canvasGroup      : {group.name} alpha={group.alpha}");
                }

                log.LogInfo("  screen children  :");
                foreach (Transform child in screen.transform)
                {
                    var childRect = child as RectTransform;
                    log.LogInfo($"    - {child.name} active={child.gameObject.activeSelf} " +
                                $"size={(childRect == null ? "n/a" : childRect.rect.size.ToString())}");
                }

                // Full tree of the chest panel, to work out whether a title can be placed
                // natively inside it rather than as an overlay plate. Layout components are
                // called out because they are what would fight an inserted element.
                var container = screen.transform.Find("ChestContainer");
                if (container != null)
                {
                    log.LogInfo("  ChestContainer tree :");
                    DumpTree(container, "    ");
                }

                log.LogInfo("--- end diagnostics ---");
            }
            catch (Exception e)
            {
                ChestLabelsPlugin.Log.LogWarning($"diagnostics dump failed: {e.Message}");
            }
        }

        /// <summary>
        /// Recursive hierarchy dump: name, size, anchored position, and any layout or mask
        /// components, which are the things that would reposition or clip an inserted title.
        /// </summary>
        private static void DumpTree(Transform node, string indent, int depth = 0)
        {
            if (depth > 3)
            {
                return;
            }

            foreach (Transform child in node)
            {
                var rect = child as RectTransform;
                var notes = string.Empty;

                foreach (var component in child.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        continue;
                    }

                    var name = component.GetType().Name;
                    if (name.Contains("Layout") || name.Contains("Mask") ||
                        name.Contains("Fitter") || name.Contains("ScrollRect"))
                    {
                        notes += " [" + name + "]";
                    }
                }

                // Anchors and pivot included because anchoredPosition alone does not say
                // where an element actually sits on screen.
                ChestLabelsPlugin.Log.LogInfo(
                    $"{indent}- {child.name}" +
                    $" size={(rect == null ? "n/a" : rect.rect.size.ToString())}" +
                    $" pos={(rect == null ? "n/a" : rect.anchoredPosition.ToString())}" +
                    $" anchors={(rect == null ? "n/a" : rect.anchorMin + "-" + rect.anchorMax)}" +
                    $" pivot={(rect == null ? "n/a" : rect.pivot.ToString())}" +
                    $" active={child.gameObject.activeSelf}{notes}");

                DumpTree(child, indent + "  ", depth + 1);
            }
        }

        private static string DescribeCorners(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return $"bottomLeft={corners[0]} topRight={corners[2]}";
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







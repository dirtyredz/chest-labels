using System;
using Chicken.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChestLabels
{
    /// <summary>
    /// Shows a chest's label in the world when the mouse is over it.
    ///
    /// The game has no hover system to hook - a search of Vampire.Runtime turns up only
    /// TelekinesisHoverEffect and SpeechHighlight - so this does its own raycast and renders
    /// onto its own overlay canvas. Owning the canvas keeps it independent of the game's UI
    /// and means nothing here can disturb a game screen.
    /// </summary>
    internal sealed class HoverLabel : MonoBehaviour
    {
        private const float RaycastDistance = 200f;
        private const float PollInterval = 0.08f;
        private const float OutlineWidth = 0.3f;
        private const float PlatePaddingX = 26f;
        private const float PlatePaddingY = 12f;
        private const float MinPlateWidth = 90f;
        private const float MaxPlateWidth = 420f;

        private Canvas canvas;
        private RectTransform plateRect;
        private Image plateBackground;
        private TextMeshProUGUI text;

        private float nextPollTime;
        private Chest currentChest;
        private bool warnedNoCamera;
        private Camera cachedCamera;
        private bool loggedFirstHit;
        private string lastShowStack;
        /// <summary>True while a label is on screen, so the interaction arrow can stand down.</summary>
        internal static bool ShowingLabel { get; private set; }

        private RectTransform nameplateAnchor;
        private string nameplateShownFor;

        private void Update()
        {
            if (!ChestLabelsPlugin.ShowHoverLabel.Value)
            {
                Hide();
                return;
            }

            // Reading the game's interaction target is a couple of field reads, so it can run
            // every frame; that keeps the label in step with the arrow instead of trailing it
            // by up to a poll interval. The raycast fallback is expensive, so it stays throttled.
            if (!UsingInteractionSource && Time.unscaledTime < nextPollTime)
            {
                // Still track the chest we already found so the label follows a moving camera.
                Reposition();
                return;
            }

            nextPollTime = Time.unscaledTime + PollInterval;

            try
            {
                Poll();
            }
            catch (Exception e)
            {
                ChestLabelsPlugin.Log.LogError($"Hover label failed; disabling it. {e}");
                ChestLabelsPlugin.ShowHoverLabel.Value = false;
                Hide();
            }
        }

        private void Poll()
        {
            LogShowStackIfChanged();

            if (ShouldStandDown())
            {
                Hide();
                return;
            }

            var camera = ResolveCamera();
            if (camera == null)
            {
                if (!warnedNoCamera)
                {
                    warnedNoCamera = true;
                    ChestLabelsPlugin.Log.LogWarning(
                        "No usable camera found - hover labels cannot position themselves.");
                }
                Hide();
                return;
            }

            // Ask the game which interactable the cursor is on, rather than raycasting
            // ourselves. The game's interaction volume is larger than a raw collider hit, so
            // two independent detections disagreed near the edge: the arrow appeared before
            // the label, then vanished as the label caught up. Sharing one source of truth
            // makes that impossible.
            var chest = FindChestFromInteractionScreen() ?? FindChestUnderMouse(camera);
            if (chest == null)
            {
                Hide();
                return;
            }

            // Without this the hover only worked after a chest had been opened, because that
            // was the only thing loading the store.
            ChestLabelsPlugin.EnsureStoreLoaded();

            var guid = GetGuid(chest);
            var label = guid == null ? null : ChestLabelsPlugin.Store?.Get(guid);
            if (string.IsNullOrEmpty(label))
            {
                Hide();
                return;
            }

            currentChest = chest;
            EnsureUi();

            if (UseGameNameplate)
            {
                ShowGameNameplate(guid, label);
            }
            else
            {
                HideGameNameplate();

                text.text = label;

                // Re-applied on every show so tuning these in the .cfg takes effect without a
                // restart - the UI objects themselves are only built once.
                ApplyHoverStyle();
                plateRect.gameObject.SetActive(true);
            }

            ShowingLabel = true;

            canvas.gameObject.SetActive(true);            Reposition();
        }

        /// <summary>
        /// Whether the hover label should keep quiet.
        ///
        /// Deliberately NOT "any screen is showing": EnergyScreen, ManaScreen and the
        /// interaction prompts are permanently in UIScreen.ShowStack during normal play, so
        /// that test suppresses the label forever. Only genuinely modal things count.
        /// </summary>
        private static bool ShouldStandDown()
        {
            // A positive gate rather than a blocklist. Logging what was on screen in each
            // situation showed PlayerCursorInteractionScreen is up exactly when the player can
            // point at the world, and absent in every case the label should stay hidden:
            //
            //   gameplay  PlayerInteractionScreen, PlayerCursorInteractionScreen, Time, Energy...
            //   chest     PlayerInteractionScreen, Time, ChestScreen          <- no cursor screen
            //   pause     PlayerInteractionScreen, ..., PauseScreen           <- no cursor screen
            //   cutscene  GameWorldUIScreen, Notification..., BatVfxScreen    <- no cursor screen
            //   menus     MenuBackgroundScreen, MainMenuScreen, ...           <- no cursor screen
            //
            // Chasing these one at a time would have meant a new special case every time
            // another screen turned up.
            var cursorScreen = UIScreen<PlayerCursorInteractionScreen>.Instance;
            return cursorScreen == null || !cursorScreen.IsShowing;
        }

        /// <summary>
        /// Log which screens are up whenever that set changes.
        ///
        /// The category map does not include dialogue or cutscene screens, so there is no
        /// existing helper that detects them. This records what is actually showing during a
        /// cutscene so the right screen can be targeted rather than guessed at.
        /// </summary>
        private void LogShowStackIfChanged()
        {
            if (!ChestLabelsPlugin.VerboseLogging.Value)
            {
                return;
            }

            var stack = UIScreen.ShowStack;
            if (stack == null)
            {
                return;
            }

            var names = string.Empty;
            foreach (var screen in stack)
            {
                if (screen != null)
                {
                    names += (names.Length > 0 ? ", " : string.Empty) + screen.GetType().Name;
                }
            }

            if (names == lastShowStack)
            {
                return;
            }

            lastShowStack = names;
            ChestLabelsPlugin.Log.LogInfo($"Screens showing: [{names}]");
        }

        /// <summary>
        /// Find the camera rendering the world.
        ///
        /// Camera.main is null in this game - the gameplay camera is not tagged "MainCamera",
        /// which is normal for a Cinemachine setup. Fall back to scanning the active cameras
        /// and taking the highest-depth one that renders to the screen, which is what the
        /// player is actually looking through.
        /// </summary>
        private Camera ResolveCamera()
        {
            if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
            {
                return cachedCamera;
            }

            var main = Camera.main;
            if (main != null)
            {
                cachedCamera = main;
                return cachedCamera;
            }

            Camera best = null;
            foreach (var candidate in Camera.allCameras)
            {
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.targetTexture != null)
                {
                    continue;
                }

                if (best == null || candidate.depth > best.depth)
                {
                    best = candidate;
                }
            }

            if (best != null && cachedCamera == null)
            {
                ChestLabelsPlugin.Log.LogInfo($"Hover label using camera '{best.name}'.");
            }

            cachedCamera = best;
            return cachedCamera;
        }

        /// <summary>
        /// Interaction colliders are frequently triggers, which a plain raycast skips - hence
        /// QueryTriggerInteraction.Collide and RaycastAll rather than the first hit only.
        /// </summary>
        /// <summary>
        /// Whether the interaction arrow should stand down this frame.
        ///
        /// Computed from the game's current interaction target rather than from whether the
        /// label happens to be on screen yet. Update order between this component and the
        /// interaction screen is undefined, and the label polls on an interval — so observing
        /// our own state left a frame or two where both the arrow and the label were visible.
        /// Deriving the answer from the same data, in the caller's frame, removes that window.
        ///
        /// Cheap enough to call every frame: two field reads and a dictionary lookup.
        /// </summary>
        internal static bool ShouldSuppressArrow()
        {
            if (!ChestLabelsPlugin.ShowHoverLabel.Value)
            {
                return false;
            }

            var chest = FindChestFromInteractionScreen();
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

        /// <summary>True when the game's interaction target is readable, so polling is cheap.</summary>
        internal static bool UsingInteractionSource => !interactionLookupUnavailable;

        private static System.Reflection.FieldInfo showingSourceField;
        private static System.Reflection.FieldInfo sourceContextField;
        private static bool interactionLookupUnavailable;

        /// <summary>
        /// The chest the game's own cursor-interaction screen is currently showing for — the
        /// same thing the interaction arrow is attached to.
        ///
        /// Using this instead of a private raycast means the label and the arrow appear and
        /// disappear together, at the game's interaction range rather than at whatever a
        /// collider hit happens to give. Returns null if unavailable, and the caller falls
        /// back to the raycast.
        /// </summary>
        private static Chest FindChestFromInteractionScreen()
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

        private Chest FindChestUnderMouse(Camera camera)
        {
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

        private void Reposition()
        {
            if (canvas == null || currentChest == null || !canvas.gameObject.activeSelf)
            {
                return;
            }

            var camera = ResolveCamera();
            if (camera == null)
            {
                return;
            }

            var anchor = currentChest.transform.position + Vector3.up * ChestLabelsPlugin.HoverHeight.Value;
            var screenPoint = camera.WorldToScreenPoint(anchor);

            if (screenPoint.z < 0f)
            {
                // Behind the camera.
                canvas.gameObject.SetActive(false);
                return;
            }

            plateRect.position = screenPoint;

            if (nameplateAnchor != null)

            {

                nameplateAnchor.position = screenPoint;

            }
        }

        /// <summary>
        /// The vanilla interaction chevron sits directly above a chest, so the hover label
        /// defaults to no background and relies on its outline for legibility instead of
        /// stamping an opaque plate over the game's own indicator.
        /// </summary>
        private void ApplyHoverStyle()
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

        private static bool UseGameNameplate =>
            ChestLabelsPlugin.UseGameNameplate.Value && UIScreen<NameplateScreen>.Instance != null;

        /// <summary>
        /// Show the label using the game's own nameplate — the same banner it uses for
        /// character names.
        ///
        /// Preferable to styling our own plate: the font, colour, banner shape and animation
        /// are the game's, and stay correct even if a patch restyles them. NameplateScreen
        /// takes arbitrary text through CustomNameplateData, and keys Show/Hide by target
        /// RectTransform — so parking our own invisible anchor at the chest's screen position
        /// is enough, and we can never disturb a nameplate the game is showing for something
        /// else.
        /// </summary>
        private void ShowGameNameplate(string guid, string label)
        {
            var screen = UIScreen<NameplateScreen>.Instance;
            if (screen == null || nameplateAnchor == null)
            {
                return;
            }

            plateRect.gameObject.SetActive(false);

            // Only call Show when the chest or its name actually changes; calling it every
            // poll would restart the reveal animation eight times a second.
            var key = guid + " " + label;
            if (key == nameplateShownFor)
            {
                return;
            }

            nameplateShownFor = key;
            screen.Show(nameplateAnchor, new CustomNameplateData(label));
            ApplyNameplateTint(screen);
        }

        private static readonly System.Collections.Generic.Dictionary<Image, Color> tintedOriginals =
            new System.Collections.Generic.Dictionary<Image, Color>();

        /// <summary>
        /// Recolour the game's nameplate bubble while it is showing our label.
        ///
        /// NameplateScreen is shared — the game uses the same bubble for its own tooltips — so
        /// the original colour of every image touched is cached and restored the moment our
        /// label goes away. Without that, the game's own nameplates would inherit our tint.
        ///
        /// The pointer arrow is included: it is part of the bubble's silhouette, so leaving it
        /// untinted left an orange spike hanging off a purple bubble.
        /// </summary>
        private static void ApplyNameplateTint(NameplateScreen screen)
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

        private static void RestoreNameplateTint()
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

        private void HideGameNameplate()
        {
            if (nameplateShownFor == null)
            {
                RestoreNameplateTint();
                return;
            }

            nameplateShownFor = null;
            RestoreNameplateTint();

            var screen = UIScreen<NameplateScreen>.Instance;
            if (screen != null && nameplateAnchor != null)
            {
                screen.Hide(nameplateAnchor, true);
            }
        }

        private void Hide()
        {
            ShowingLabel = false;
            HideGameNameplate();

            currentChest = null;
            if (canvas != null)
            {
                canvas.gameObject.SetActive(false);
            }
        }

        private void EnsureUi()
        {
            if (canvas != null)
            {
                return;
            }

            var canvasGo = new GameObject("ChestLabels_HoverCanvas");
            canvasGo.transform.SetParent(transform, false);
            DontDestroyOnLoad(canvasGo);

            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above normal UI but below anything that deliberately claims the top.
            canvas.sortingOrder = 500;
            canvasGo.AddComponent<CanvasScaler>();

            var plate = new GameObject("Plate");
            plate.transform.SetParent(canvasGo.transform, false);

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

            // Invisible anchor the game's nameplate attaches to; moved to the chest's
            // screen position each frame.
            var anchorGo = new GameObject("NameplateAnchor");
            anchorGo.transform.SetParent(canvasGo.transform, false);
            nameplateAnchor = anchorGo.AddComponent<RectTransform>();
            nameplateAnchor.sizeDelta = new Vector2(1f, 1f);
            nameplateAnchor.pivot = new Vector2(0.5f, 0.5f);

            canvasGo.SetActive(false);
            ChestLabelsPlugin.Log.LogInfo("Hover label canvas created.");
        }

        private static string GetGuid(Chest chest) => ChestIdentity.GuidOf(chest);
    }
}








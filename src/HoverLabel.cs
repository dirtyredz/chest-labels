using System;
using Chicken.UI;
using UnityEngine;

namespace ChestLabels
{
    /// <summary>
    /// Shows a chest's label in the world when the mouse is over it.
    ///
    /// The game has no hover system to hook - a search of Vampire.Runtime turns up only
    /// TelekinesisHoverEffect and SpeechHighlight - so this owns its own overlay canvas and
    /// update loop. This type is the orchestrator: it polls, resolves the camera, and drives the
    /// collaborators. Detection lives in <see cref="ChestInteractionSource"/>, the mod's own
    /// plate in <see cref="HoverLabelPlateView"/>, and the game-nameplate path in
    /// <see cref="GameNameplateView"/>.
    /// </summary>
    internal sealed class HoverLabel : MonoBehaviour
    {
        private const float PollInterval = 0.08f;

        private readonly HoverLabelPlateView plateView = new HoverLabelPlateView();
        private readonly GameNameplateView nameplateView = new GameNameplateView();

        private Canvas canvas;
        private RectTransform nameplateAnchor;

        private float nextPollTime;
        private Chest currentChest;
        private bool warnedNoCamera;
        private Camera cachedCamera;
        private string lastShowStack;

        /// <summary>Delegates to <see cref="ChestInteractionSource"/> for the arrow-hiding patch.</summary>
        internal static bool ShouldSuppressArrow() => ChestInteractionSource.ShouldSuppressArrow();

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
            if (!ChestInteractionSource.UsingInteractionSource && Time.unscaledTime < nextPollTime)
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

            if (ChestInteractionSource.ShouldStandDown())
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

            var chest = ChestInteractionSource.FindChest(camera);
            if (chest == null)
            {
                Hide();
                return;
            }

            // Without this the hover only worked after a chest had been opened, because that
            // was the only thing loading the store.
            ChestLabelsPlugin.EnsureStoreLoaded();

            var guid = ChestInteractionSource.GetGuid(chest);
            var label = guid == null ? null : ChestLabelsPlugin.Store?.Get(guid);
            if (string.IsNullOrEmpty(label))
            {
                Hide();
                return;
            }

            currentChest = chest;
            EnsureUi();

            if (ChestLabelsPlugin.UseGameNameplate.Value && GameNameplateView.IsAvailable)
            {
                plateView.Hide();
                nameplateView.Show(nameplateAnchor, guid, label);
            }
            else
            {
                nameplateView.Hide(nameplateAnchor);
                plateView.Show(label);
            }

            canvas.gameObject.SetActive(true);
            Reposition();
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

            var world = currentChest.transform.position + Vector3.up * ChestLabelsPlugin.HoverHeight.Value;
            var screenPoint = camera.WorldToScreenPoint(world);

            if (screenPoint.z < 0f)
            {
                // Behind the camera.
                canvas.gameObject.SetActive(false);
                return;
            }

            plateView.Reposition(screenPoint);
            if (nameplateAnchor != null)
            {
                nameplateAnchor.position = screenPoint;
            }
        }

        private void Hide()
        {
            nameplateView.Hide(nameplateAnchor);

            currentChest = null;
            if (canvas != null)
            {
                canvas.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Build the shared overlay canvas and the nameplate anchor once. The plate view builds
        /// its own subtree under the canvas; the game-nameplate view uses the anchor.
        /// </summary>
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
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();

            plateView.Ensure(canvasGo.transform);

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
    }
}

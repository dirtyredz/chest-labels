using System;
using TMPro;
using UnityEngine;

namespace ChestLabels
{
    /// <summary>
    /// One-shot diagnostic dumps for the chest window, gated behind
    /// <c>ChestLabelsPlugin.LogUiDiagnostics</c>. Pure logging - it reads the UI tree and
    /// prints it, never mutating anything. Costs a little log noise and saves a lot of guessing
    /// when a game update reshapes the panel; turn it off once positioning is right.
    /// </summary>
    internal static class UiDiagnostics
    {
        /// <summary>
        /// Dump the chest panel's geometry (SlotContainer / Header / Layout / Footer anchors),
        /// which is what resizing the header area depends on. Dumps from the panel root, not the
        /// band, so those siblings are visible.
        /// </summary>
        public static void DumpPanelGeometry(Transform band)
        {
            var panel = band.parent != null ? band.parent.parent : band;
            ChestLabelsPlugin.Log.LogInfo("--- chest panel geometry ---");
            DumpTree(panel != null ? panel : band, "    ");
            ChestLabelsPlugin.Log.LogInfo("--- end geometry ---");
        }

        /// <summary>
        /// One-shot dump of everything that decides whether the header is visible. Turn off once
        /// positioning is right.
        /// </summary>
        public static void DumpHeaderDiagnostics(ChestScreen screen, TextMeshProUGUI text)
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
    }
}

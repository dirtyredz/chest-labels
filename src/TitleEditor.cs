using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChestLabels
{
    /// <summary>
    /// In-place renaming from the chest screen: a pencil button beside the title swaps it for
    /// a text field, Enter commits, Escape cancels.
    ///
    /// Uses a plain TMP_InputField, which is what the game's own InputFieldNavigator wraps for
    /// naming animals, so the existing EventSystem drives it without extra setup.
    /// </summary>
    internal static class TitleEditor
    {
        private const float ButtonSize = 40f;

        internal const string ButtonObjectName = "ChestLabels_EditButton";
        internal const string InputObjectName = "ChestLabels_Input";

        /// <summary>
        /// True while the field has focus. The chest screen reads its own hotkeys through
        /// Rewired, which ignores Unity's EventSystem focus entirely - so without this gate a
        /// name containing "x" would trigger "reorder chest" as you typed it.
        /// </summary>
        internal static bool IsEditing { get; private set; }

        private static TMP_InputField input;
        private static TextMeshProUGUI titleText;
        private static string editingGuid;

        /// <summary>Build (or refresh) the pencil button and hidden input field in the band.</summary>
        internal static void Attach(Transform band, TextMeshProUGUI title, string chestGuid, string label)
        {
            titleText = title;
            editingGuid = chestGuid;

            var button = band.Find(ButtonObjectName);
            if (button == null)
            {
                button = CreateButton(band, title != null ? title.font : null);
            }

            button.gameObject.SetActive(chestGuid != null);

            // Re-applied rather than set once at creation, so changing the size from an
            // in-game config menu takes effect on the next chest instead of needing a restart.
            if (button is RectTransform buttonRect)
            {
                var size = ButtonSize;
                buttonRect.sizeDelta = new Vector2(size, size);
                buttonRect.anchoredPosition = new Vector2(-6f, ChestLabelsPlugin.TitleOffsetY.Value);
            }

            if (band.Find(InputObjectName) == null)
            {
                CreateInput(band, title);
            }
            else if (input != null && input.textComponent is TextMeshProUGUI inputText)
            {
                // Re-applied so a size change in settings reaches the field too, rather than
                // only the title.
                ApplyInputTextStyle(inputText);
            }

            // A chest with no name still needs a way to get one, so the button stays visible
            // and the placeholder invites a name.
            if (input != null)
            {
                input.gameObject.SetActive(false);
                input.text = label ?? string.Empty;
            }
        }
        private static Transform CreateButton(Transform band, TMP_FontAsset font)
        {
            var go = new GameObject(ButtonObjectName);
            go.transform.SetParent(band, false);
            go.transform.SetAsLastSibling();

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            var size = ButtonSize;
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(-6f, ChestLabelsPlugin.TitleOffsetY.Value);

            // Transparent hit area only - no visible frame. An alpha-0 Image still receives
            // raycasts, so this gives the button a click target without drawing anything.
            var hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(go.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(5f, 5f);
            iconRect.offsetMax = new Vector2(-5f, -5f);

            var icon = iconGo.AddComponent<Image>();
            icon.sprite = PencilIcon.Get();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var button = go.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None; // HoverFeedback drives the visuals
            button.onClick.AddListener(BeginEdit);

            // Tint the pencil itself, since there is no plate to light up.
            var feedback = go.AddComponent<HoverFeedback>();
            feedback.Backing = icon;

            ChestLabelsPlugin.Log.LogInfo($"Edit button created ({size}px) with generated pencil sprite.");

            return go.transform;
        }

        /// <summary>Keep the edit field visually identical to the title it replaces.</summary>
        private static void ApplyInputTextStyle(TextMeshProUGUI text)
        {
            if (text == null)
            {
                return;
            }

            text.color = GamePalette.CountGold;
            text.enableAutoSizing = true;
            text.fontSizeMax = ChestLabelsPlugin.HeaderFontSize.Value;
            text.fontSizeMin = 12f;
        }

        private static void CreateInput(Transform band, TextMeshProUGUI title)
        {
            var go = new GameObject(InputObjectName);
            go.transform.SetParent(band, false);
            go.transform.SetAsLastSibling();

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(-56f, ChestLabelsPlugin.HeaderFontSize.Value + 12f);
            rect.anchoredPosition = new Vector2(-14f, ChestLabelsPlugin.TitleOffsetY.Value);

            var background = go.AddComponent<Image>();
            background.color = new Color32(12, 6, 24, 200);

            var textArea = new GameObject("Text");
            textArea.transform.SetParent(go.transform, false);
            var textRect = textArea.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 2f);
            textRect.offsetMax = new Vector2(-8f, -2f);

            var text = textArea.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;

            // Must match the title exactly, or clicking the pencil visibly swaps the weight,
            // size and colour mid-edit. Same heavy weight, same gold, same size.
            GameFonts.Apply(text, preferOutline: false, heavy: true);
            ApplyInputTextStyle(text);

            input = go.AddComponent<TMP_InputField>();
            input.textComponent = text;
            input.textViewport = textRect;
            input.characterLimit = LabelStore.MaxLabelLength;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.onSubmit.AddListener(_ => Commit());
            input.onDeselect.AddListener(_ => Commit());

            go.SetActive(false);
        }

        private static void BeginEdit()
        {
            if (input == null || editingGuid == null)
            {
                return;
            }

            try
            {
                input.text = ChestLabelsPlugin.Store?.Get(editingGuid) ?? string.Empty;
                input.gameObject.SetActive(true);

                if (titleText != null)
                {
                    titleText.gameObject.SetActive(false);
                }

                IsEditing = true;

                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(input.gameObject);
                }
                input.ActivateInputField();
                input.caretPosition = input.text.Length;

                ChestLabelsPlugin.Log.LogInfo($"Editing label for chest {editingGuid}.");
            }
            catch (Exception e)
            {
                IsEditing = false;
                ChestLabelsPlugin.Log.LogError($"Could not start editing: {e}");
            }
        }

        internal static void Commit()
        {
            if (!IsEditing)
            {
                return;
            }

            var value = input != null ? input.text : null;
            EndEdit();

            try
            {
                if (ChestLabelsPlugin.Store == null || editingGuid == null)
                {
                    return;
                }

                if (ChestLabelsPlugin.Store.Set(editingGuid, value))
                {
                    ChestLabelsPlugin.Store.Save();
                    ChestLabelsPlugin.Log.LogInfo(
                        string.IsNullOrWhiteSpace(value)
                            ? $"Cleared label for chest {editingGuid}."
                            : $"Chest {editingGuid} renamed to \"{value.Trim()}\".");
                }

                RefreshTitle();
            }
            catch (Exception e)
            {
                ChestLabelsPlugin.Log.LogError($"Could not save label: {e}");
            }
        }

        internal static void Cancel()
        {
            if (!IsEditing)
            {
                return;
            }

            EndEdit();
            RefreshTitle();
        }

        private static void EndEdit()
        {
            IsEditing = false;

            if (input != null)
            {
                input.DeactivateInputField();
                input.gameObject.SetActive(false);
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private static void RefreshTitle()
        {
            if (titleText == null)
            {
                return;
            }

            var label = editingGuid == null ? null : ChestLabelsPlugin.Store?.Get(editingGuid);

            if (string.IsNullOrEmpty(label))
            {
                titleText.gameObject.SetActive(false);
            }
            else
            {
                titleText.text = label;
                titleText.gameObject.SetActive(true);
            }
        }
    }
}



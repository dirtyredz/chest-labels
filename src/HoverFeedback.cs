using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChestLabels
{
    /// <summary>
    /// Hover and press feedback for the edit button.
    ///
    /// Unity's built-in ColorBlock tint alone was too subtle to signal that the pencil is
    /// clickable, so this drives a visible backing circle and a small scale bump as well.
    /// </summary>
    internal sealed class HoverFeedback : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float RestScale = 1f;
        private const float HoverScale = 1.18f;
        private const float PressScale = 1.05f;
        private const float Speed = 14f;

        /// <summary>
        /// The graphic that gets tinted. With no backing plate this is the icon itself, so
        /// the affordance comes from the icon growing and brightening rather than from a
        /// frame around it.
        /// </summary>
        internal Graphic Backing;

        internal Color RestColour = new Color(1f, 1f, 1f, 0.82f);
        internal Color HoverColour = Color.white;
        internal Color PressColour = new Color(0.85f, 0.85f, 0.85f, 1f);

        private bool hovered;
        private bool pressed;
        private float targetScale = RestScale;
        private Color targetColour;

        private void Awake()
        {
            targetColour = RestColour;
            if (Backing != null)
            {
                Backing.color = RestColour;
            }
        }

        private void Update()
        {
            // Unscaled: the chest screen may be open while the game clock is paused.
            var t = 1f - Mathf.Exp(-Speed * Time.unscaledDeltaTime);

            transform.localScale = Vector3.Lerp(
                transform.localScale, Vector3.one * targetScale, t);

            if (Backing != null)
            {
                Backing.color = Color.Lerp(Backing.color, targetColour, t);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
            Refresh();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
            Refresh();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            Refresh();
        }

        private void Refresh()
        {
            if (pressed)
            {
                targetScale = PressScale;
                targetColour = PressColour;
            }
            else if (hovered)
            {
                targetScale = HoverScale;
                targetColour = HoverColour;
            }
            else
            {
                targetScale = RestScale;
                targetColour = RestColour;
            }
        }
    }
}

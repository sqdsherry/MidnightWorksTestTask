using UnityEngine;

namespace AutoService.Presentation.Ui
{
    /// <summary>Shows screen views however their objects were left in the scene.</summary>
    public static class UiVisibility
    {
        /// <summary>
        /// Activates <paramref name="target"/> and every inactive parent up to the scene root.
        /// </summary>
        /// <remarks>
        /// Why: a panel switched off by hand in the editor (or a parent switched off while laying out the canvas) must
        /// still appear when the presenter shows it — the same rule as <c>ScreenAnchoredPanel</c>. Allocation-free.
        /// </remarks>
        /// <param name="target">The object to show; ignored when null.</param>
        public static void ShowChain(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            // Show parents first without animation
            for (Transform current = target.transform.parent; current != null; current = current.parent)
            {
                if (!current.gameObject.activeSelf)
                {
                    current.gameObject.SetActive(true);
                }
            }

            var animator = target.GetComponent<UiWindowAnimator>();
            if (animator != null)
            {
                animator.Show();
            }
            else if (!target.activeSelf)
            {
                target.SetActive(true);
            }
        }

        /// <summary>Deactivates <paramref name="target"/>; ignored when null.</summary>
        public static void Hide(GameObject target)
        {
            if (target != null && target.activeSelf)
            {
                var animator = target.GetComponent<UiWindowAnimator>();
                if (animator != null)
                {
                    // Animator will deactivate it upon completion
                    animator.Hide();
                }
                else
                {
                    target.SetActive(false);
                }
            }
        }
    }
}

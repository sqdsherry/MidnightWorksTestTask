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

            for (Transform current = target.transform; current != null; current = current.parent)
            {
                if (!current.gameObject.activeSelf)
                {
                    current.gameObject.SetActive(true);
                }
            }
        }

        /// <summary>Deactivates <paramref name="target"/>; ignored when null.</summary>
        public static void Hide(GameObject target)
        {
            if (target != null && target.activeSelf)
            {
                target.SetActive(false);
            }
        }
    }
}

namespace AutoService.Presentation.Interaction
{
    /// <summary>Helpers for working with <see cref="IInteractable"/> references.</summary>
    public static class InteractableExtensions
    {
        /// <summary>
        /// True if <paramref name="interactable"/> is non-null and, when it is a Unity object, not destroyed.
        /// </summary>
        /// <remarks>
        /// Why: an interface reference bypasses Unity's overloaded <c>==</c>, so a destroyed MonoBehaviour
        /// still looks non-null to <c>?.</c> / <c>!= null</c> and calling into it throws <c>MissingReferenceException</c>.
        /// </remarks>
        public static bool IsAlive(this IInteractable interactable)
        {
            if (interactable is UnityEngine.Object unityObject)
            {
                return unityObject != null;
            }

            return interactable != null;
        }
    }
}

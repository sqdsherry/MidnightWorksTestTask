using UnityEngine;

namespace AutoService.Presentation.Interaction
{
    /// <summary>
    /// Anything the character can walk up to and interact with (service points, the warehouse, debug objects).
    /// </summary>
    /// <remarks>
    /// Lifecycle driven by the character: <see cref="BeginInteraction"/> once it has arrived and turned,
    /// <see cref="EndInteraction"/> when it leaves because of a new command. The two calls are always paired.
    /// </remarks>
    public interface IInteractable
    {
        /// <summary>World position the character walks to.</summary>
        Vector3 ApproachPosition { get; }

        /// <summary>Rotation the character takes after arriving (faces the object).</summary>
        Quaternion ApproachRotation { get; }

        /// <summary>False → clicks on it are ignored and it is not highlighted (e.g. locked/unbuilt later).</summary>
        bool IsInteractable { get; }

        /// <summary>Hover feedback.</summary>
        /// <param name="highlighted">True when the pointer is over the object.</param>
        void SetHighlighted(bool highlighted);

        /// <summary>The character arrived and starts interacting (module 03: occupies the work spot).</summary>
        void BeginInteraction();

        /// <summary>The character left (new command) — interaction ends.</summary>
        void EndInteraction();
    }
}

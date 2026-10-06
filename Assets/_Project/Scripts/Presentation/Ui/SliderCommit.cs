using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AutoService.Presentation.Ui
{
    /// <summary>
    /// Put next to a <see cref="UnityEngine.UI.Slider"/>: tells when the player lets go of it, so a value previewed while
    /// dragging can be committed (saved) once instead of every frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SliderCommit : MonoBehaviour, IPointerUpHandler
    {
        /// <summary>Raised when the pointer is released over or after dragging the slider.</summary>
        public event Action Released;

        /// <inheritdoc />
        public void OnPointerUp(PointerEventData eventData) => Released?.Invoke();
    }
}

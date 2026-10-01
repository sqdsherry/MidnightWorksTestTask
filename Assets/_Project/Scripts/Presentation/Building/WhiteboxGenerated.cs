using UnityEngine;

namespace AutoService.Presentation.Building
{
    /// <summary>
    /// Marker on objects created by the whitebox layout builder (copied bays, ghosts), so a re-run can find and replace
    /// exactly them. No behaviour; safe to remove together with the object once real assets replace the whitebox.
    /// </summary>
    /// <remarks>Why a runtime component: an Editor-only script on a scene object would be a missing script in a build.</remarks>
    [DisallowMultipleComponent]
    public sealed class WhiteboxGenerated : MonoBehaviour
    {
    }
}

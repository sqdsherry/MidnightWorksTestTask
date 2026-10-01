namespace AutoService.Presentation.Interaction
{
    /// <summary>Which material property <see cref="InteractableHighlight"/> drives.</summary>
    public enum HighlightProperty
    {
        /// <summary>
        /// URP Lit <c>_EmissionColor</c> = highlight color × intensity. Keeps the object's own color and reads well
        /// in any lighting, but the material must have Emission enabled (the <c>_EMISSION</c> keyword cannot be
        /// toggled through a property block).
        /// </summary>
        Emission = 0,

        /// <summary>
        /// <c>_BaseColor</c> lerped from the material's base color towards the highlight color by intensity.
        /// Works with any URP Lit/Unlit material, at the cost of changing the object's albedo.
        /// </summary>
        BaseColorTint = 1,
    }
}

namespace AutoService.Presentation.Controls
{
    /// <summary>Something that closes on Esc while it is open (a panel, the pause menu).</summary>
    /// <remarks>Registered in the <see cref="EscapeRouter"/> when it opens and removed when it closes.</remarks>
    public interface IEscapeHandler
    {
        /// <summary>Reacts to Esc (usually by closing).</summary>
        /// <returns>True if Esc was used; false lets the handler below (or the pause menu) have it.</returns>
        bool TryHandleEscape();
    }
}

namespace AutoService.Presentation.Player
{
    /// <summary>Kinds of commands <see cref="PlayerView"/> hands to <see cref="PlayerMotor"/>.</summary>
    internal enum PlayerCommandKind
    {
        /// <summary>Walk to a point.</summary>
        MoveTo = 0,

        /// <summary>Walk to an interactable, turn, interact.</summary>
        Approach = 1,

        /// <summary>Stop where standing.</summary>
        Stop = 2,
    }
}

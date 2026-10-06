namespace AutoService.Services.Audio
{
    /// <summary>Sound effects the game can play. Values index the clip table of the audio implementation.</summary>
    public enum SfxKind
    {
        /// <summary>Any UI button press.</summary>
        ButtonClick = 0,

        /// <summary>Money earned.</summary>
        Cash = 1,

        /// <summary>A service point finished working on a car.</summary>
        ServiceCompleted = 2,

        /// <summary>The player reached a new level.</summary>
        LevelUp = 3,
    }
}

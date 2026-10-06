namespace AutoService.Services.Progression
{
    /// <summary>
    /// Event fired when the player reaches a new level.
    /// </summary>
    public readonly struct LevelUpEvent
    {
        public readonly int NewLevel;

        public LevelUpEvent(int newLevel)
        {
            NewLevel = newLevel;
        }
    }
}

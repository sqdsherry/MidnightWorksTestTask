namespace AutoService.Services.Menu
{
    /// <summary>Outcome of <see cref="MainMenuModel.NewGame"/>.</summary>
    public enum NewGameResult
    {
        /// <summary>There was no progress to lose: the game is loading.</summary>
        Started = 0,

        /// <summary>A save exists: nothing happened, the player has to confirm with <see cref="MainMenuModel.ConfirmNewGame"/>.</summary>
        NeedsConfirmation = 1,
    }
}

namespace AutoService.Services.Scenes
{
    /// <summary>Scenes the player can be sent to. Boot is not here: it is loaded once by the engine and never again.</summary>
    public enum GameScene
    {
        /// <summary>Main menu: Continue / New Game / Settings / Quit.</summary>
        MainMenu = 0,

        /// <summary>The game itself.</summary>
        Gameplay = 1,
    }
}

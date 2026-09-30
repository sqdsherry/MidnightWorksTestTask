namespace AutoService.Services.Config
{
    /// <summary>
    /// Read-only game configuration exposed to the game in engine-agnostic form.
    /// Domain and Services never see ScriptableObjects; Infrastructure maps them into these settings once.
    /// </summary>
    public interface IConfigProvider
    {
        /// <summary>Global economy settings.</summary>
        EconomySettings Economy { get; }
    }
}

namespace AutoService.Services.Save
{
    /// <summary>
    /// Converts <see cref="SaveData"/> to and from text.
    /// </summary>
    /// <remarks>
    /// Why an interface: the real implementation uses JsonUtility, which lives in <c>UnityEngine</c>,
    /// so the Services assembly can only see the contract.
    /// </remarks>
    public interface ISaveSerializer
    {
        /// <summary>Serializes <paramref name="data"/>.</summary>
        string Serialize(SaveData data);

        /// <summary>Parses <paramref name="json"/>.</summary>
        /// <param name="json">Text produced by <see cref="Serialize"/>; may be empty or corrupted.</param>
        /// <param name="data">The parsed snapshot, or null on failure.</param>
        /// <returns>False if the text is empty or cannot be parsed.</returns>
        bool TryDeserialize(string json, out SaveData data);
    }
}

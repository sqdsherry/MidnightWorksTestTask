using AutoService.Services.Save;

namespace AutoService.Tests.EditMode
{
    /// <summary>
    /// <see cref="ISaveSerializer"/> that returns a prepared snapshot, for shapes the real serializer cannot produce
    /// (e.g. null arrays).
    /// </summary>
    public sealed class FakeSaveSerializer : ISaveSerializer
    {
        /// <summary>Returned by <see cref="TryDeserialize"/>; null makes it fail.</summary>
        public SaveData Result { get; set; }

        /// <inheritdoc />
        public string Serialize(SaveData data) => "{}";

        /// <inheritdoc />
        public bool TryDeserialize(string json, out SaveData data)
        {
            data = Result;
            return data != null;
        }
    }
}

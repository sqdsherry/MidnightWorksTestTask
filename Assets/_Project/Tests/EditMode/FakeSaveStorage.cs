using AutoService.Services.Save;

namespace AutoService.Tests.EditMode
{
    /// <summary>In-memory <see cref="ISaveStorage"/>.</summary>
    public sealed class FakeSaveStorage : ISaveStorage
    {
        /// <summary>The stored text; null means nothing is stored.</summary>
        public string Json { get; set; }

        /// <summary>Number of <see cref="Write"/> calls.</summary>
        public int WriteCount { get; private set; }

        /// <inheritdoc />
        public bool Exists => Json != null;

        /// <inheritdoc />
        public bool TryRead(out string json)
        {
            json = Json;
            return json != null;
        }

        /// <inheritdoc />
        public void Write(string json)
        {
            WriteCount++;
            Json = json;
        }

        /// <inheritdoc />
        public void Delete() => Json = null;
    }
}

using System.Collections.Generic;
using AutoService.Services.Save;

namespace AutoService.Tests.EditMode
{
    /// <summary>In-memory <see cref="ISaveService"/> that records every save.</summary>
    public sealed class FakeSaveService : ISaveService
    {
        /// <summary>Snapshot returned by <see cref="TryLoad"/>; null means there is no save.</summary>
        public SaveData Stored { get; set; }

        /// <summary>Every snapshot passed to <see cref="Save"/>, in order.</summary>
        public List<SaveData> Saved { get; } = new List<SaveData>();

        /// <summary>Number of <see cref="Delete"/> calls.</summary>
        public int DeleteCount { get; private set; }

        /// <inheritdoc />
        public bool HasSave => Stored != null;

        /// <inheritdoc />
        public bool TryLoad(out SaveData data)
        {
            data = Stored;
            return data != null;
        }

        /// <inheritdoc />
        public void Save(SaveData data)
        {
            Saved.Add(data);
            Stored = data;
        }

        /// <inheritdoc />
        public void Delete()
        {
            DeleteCount++;
            Stored = null;
        }
    }
}

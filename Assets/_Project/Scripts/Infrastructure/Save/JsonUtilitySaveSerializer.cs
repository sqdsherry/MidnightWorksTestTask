using System;
using AutoService.Services.Save;
using UnityEngine;

namespace AutoService.Infrastructure.Save
{
    /// <summary>
    /// <see cref="ISaveSerializer"/> backed by Unity's built-in <see cref="JsonUtility"/> (no third-party JSON library).
    /// </summary>
    public sealed class JsonUtilitySaveSerializer : ISaveSerializer
    {
        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        public string Serialize(SaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            // Why: compact output; the file is not meant to be edited by hand and smaller writes are faster.
            return JsonUtility.ToJson(data, prettyPrint: false);
        }

        /// <inheritdoc />
        public bool TryDeserialize(string json, out SaveData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (ArgumentException)
            {
                // JsonUtility reports malformed JSON with an ArgumentException; the caller logs the failure.
                data = null;
                return false;
            }

            return data != null;
        }
    }
}

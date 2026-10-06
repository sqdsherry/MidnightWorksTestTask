using System;
using AutoService.Services.Save;

namespace AutoService.Tests.EditMode
{
    /// <summary><see cref="IGameSaver"/> that counts saves and can run a check at the moment of saving.</summary>
    public sealed class FakeGameSaver : IGameSaver
    {
        private readonly Action _onSave;

        /// <summary>Creates the fake.</summary>
        /// <param name="onSave">Runs inside every <see cref="SaveNow"/>; may be null.</param>
        public FakeGameSaver(Action onSave)
        {
            _onSave = onSave;
        }

        /// <summary>Number of <see cref="SaveNow"/> calls.</summary>
        public int SaveCount { get; private set; }

        /// <inheritdoc />
        public void SaveNow()
        {
            SaveCount++;
            _onSave?.Invoke();
        }

        /// <inheritdoc />
        public void ResetProgress()
        {
        }
    }
}

using System;
using System.Collections.Generic;
using AutoService.Services.Save;

namespace AutoService.Tests.EditMode
{
    /// <summary>
    /// <see cref="ISaveable"/> that appends "capture:name" / "restore:name" to a log shared between instances,
    /// so tests can check the call order. Can be told to throw.
    /// </summary>
    public sealed class FakeSaveable : ISaveable
    {
        private readonly string _name;
        private readonly List<string> _log;

        /// <summary>Creates the saveable.</summary>
        /// <param name="name">Name written to the log.</param>
        /// <param name="log">Log shared with other saveables of the same test.</param>
        public FakeSaveable(string name, List<string> log)
        {
            _name = name;
            _log = log;
        }

        /// <summary>When true, <see cref="Capture"/> and <see cref="Restore"/> throw after logging.</summary>
        public bool Throws { get; set; }

        /// <summary>Value written to <see cref="SaveData.tutorialStep"/> on capture.</summary>
        public int TutorialStepToWrite { get; set; }

        /// <summary>Snapshot passed to the last <see cref="Restore"/> call.</summary>
        public SaveData Restored { get; private set; }

        /// <inheritdoc />
        public void Capture(SaveData data)
        {
            _log.Add("capture:" + _name);
            if (Throws)
            {
                throw new InvalidOperationException("Capture failed on purpose.");
            }

            data.tutorialStep = TutorialStepToWrite;
        }

        /// <inheritdoc />
        public void Restore(SaveData data)
        {
            _log.Add("restore:" + _name);
            if (Throws)
            {
                throw new InvalidOperationException("Restore failed on purpose.");
            }

            Restored = data;
        }
    }
}

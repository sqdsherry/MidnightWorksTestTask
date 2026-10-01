using System;
using System.IO;
using AutoService.Infrastructure.Save;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="FileSaveStorage"/> on a unique temporary folder, deleted after each test.</summary>
    public sealed class FileSaveStorageTests
    {
        private string _directory;
        private string _mainPath;
        private FakeGameLogger _logger;
        private FileSaveStorage _storage;

        [SetUp]
        public void SetUp()
        {
            // Not created here on purpose: the first write must create it.
            _directory = Path.Combine(Path.GetTempPath(), "AutoServiceSaveTests_" + Guid.NewGuid().ToString("N"));
            _mainPath = Path.Combine(_directory, FileSaveStorage.DefaultFileName);
            _logger = new FakeGameLogger();
            _storage = new FileSaveStorage(_directory, FileSaveStorage.DefaultFileName, _logger);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Test]
        public void Write_ThenRead_ReturnsSameText()
        {
            Assert.IsFalse(_storage.Exists);

            _storage.Write("{\"money\":1}");

            Assert.IsTrue(_storage.Exists);
            Assert.IsTrue(_storage.TryRead(out string json));
            Assert.AreEqual("{\"money\":1}", json);
            Assert.IsFalse(File.Exists(_mainPath + ".tmp"));
            Assert.AreEqual(0, _logger.Errors.Count);
        }

        [Test]
        public void SecondWrite_KeepsPreviousAsBackup()
        {
            _storage.Write("first");
            _storage.Write("second");

            Assert.IsTrue(_storage.TryRead(out string json));
            Assert.AreEqual("second", json);
            Assert.AreEqual("first", File.ReadAllText(_mainPath + ".bak"));
            Assert.IsFalse(File.Exists(_mainPath + ".tmp"));
            Assert.AreEqual(0, _logger.Errors.Count);
        }

        [Test]
        public void TryRead_MainMissing_ReadsBackup()
        {
            _storage.Write("first");
            _storage.Write("second");
            File.Delete(_mainPath);

            Assert.IsTrue(_storage.Exists);
            Assert.IsTrue(_storage.TryRead(out string json));
            Assert.AreEqual("first", json);
            Assert.AreEqual(1, _logger.Warnings.Count);
        }

        [Test]
        public void TryRead_NothingWritten_ReturnsFalse()
        {
            Assert.IsFalse(_storage.TryRead(out string json));
            Assert.IsNull(json);
            Assert.AreEqual(0, _logger.Errors.Count);
        }

        [Test]
        public void Delete_RemovesMainAndBackup()
        {
            _storage.Write("first");
            _storage.Write("second");

            _storage.Delete();

            Assert.IsFalse(_storage.Exists);
            Assert.IsFalse(_storage.TryRead(out string _));
            Assert.IsFalse(File.Exists(_mainPath));
            Assert.IsFalse(File.Exists(_mainPath + ".bak"));
            Assert.AreEqual(0, _logger.Errors.Count);
        }
    }
}

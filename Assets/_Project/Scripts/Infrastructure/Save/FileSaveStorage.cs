using System;
using System.IO;
using System.Security;
using AutoService.Services.Core;
using AutoService.Services.Save;

namespace AutoService.Infrastructure.Save
{
    /// <summary>
    /// <see cref="ISaveStorage"/> that keeps the save in a single file with atomic replacement and a one-step backup:
    /// <c>save.json</c> (current), <c>save.json.tmp</c> (being written), <c>save.json.bak</c> (previous).
    /// </summary>
    /// <remarks>
    /// The directory is passed in (the entry point uses <c>Application.persistentDataPath</c>) instead of being read
    /// from <c>Application</c> here, so the class can be tested on a temporary folder.
    /// <para>
    /// Why only <c>System.IO</c> with full paths built by <see cref="Path.Combine(string, string)"/>:
    /// <c>persistentDataPath</c> of a Windows user can contain non-ASCII characters (e.g. a Cyrillic user name),
    /// and hand-made "/" concatenation or engine file APIs are where such paths tend to break.
    /// </para>
    /// </remarks>
    public sealed class FileSaveStorage : ISaveStorage
    {
        /// <summary>Conventional save file name.</summary>
        public const string DefaultFileName = "save.json";

        private const string TempSuffix = ".tmp";
        private const string BackupSuffix = ".bak";

        private readonly string _directory;
        private readonly string _mainPath;
        private readonly string _tempPath;
        private readonly string _backupPath;
        private readonly IGameLogger _logger;

        /// <summary>Creates the storage. Touches no files until the first call.</summary>
        /// <param name="directory">Folder of the save; created on the first write if missing.</param>
        /// <param name="fileName">Save file name, e.g. <see cref="DefaultFileName"/>.</param>
        /// <param name="logger">Receives every I/O error (they are never thrown to the caller).</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="directory"/> or <paramref name="fileName"/> is empty.</exception>
        public FileSaveStorage(string directory, string fileName, IGameLogger logger)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("Save directory must not be empty.", nameof(directory));
            }

            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("Save file name must not be empty.", nameof(fileName));
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _directory = Path.GetFullPath(directory);
            _mainPath = Path.Combine(_directory, fileName);
            _tempPath = _mainPath + TempSuffix;
            _backupPath = _mainPath + BackupSuffix;
        }

        /// <inheritdoc />
        /// <remarks>True also when only the backup is left (it is what <see cref="TryRead"/> would return).</remarks>
        public bool Exists => File.Exists(_mainPath) || File.Exists(_backupPath);

        /// <inheritdoc />
        /// <remarks>Falls back to the backup when the main file is missing (e.g. after a crash between two steps of a write).</remarks>
        public bool TryRead(out string json)
        {
            json = null;
            try
            {
                if (File.Exists(_mainPath))
                {
                    json = File.ReadAllText(_mainPath);
                    return true;
                }

                if (File.Exists(_backupPath))
                {
                    _logger.Warning("[Save] Main save file is missing; reading the backup '" + _backupPath + "'.");
                    json = File.ReadAllText(_backupPath);
                    return true;
                }

                return false;
            }
            catch (Exception exception) when (IsIoError(exception))
            {
                _logger.Error("[Save] Failed to read the save from '" + _mainPath + "': " + exception.Message);
                json = null;
                return false;
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Why write-then-replace: a crash or power loss in the middle of writing must not leave a half-written
        /// (corrupted) save. The text first goes to the temp file; only a complete temp file replaces the main one,
        /// and the replaced file is kept as the backup.
        /// </remarks>
        public void Write(string json)
        {
            try
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllText(_tempPath, json ?? string.Empty);

                if (File.Exists(_mainPath))
                {
                    File.Replace(_tempPath, _mainPath, _backupPath);
                }
                else
                {
                    File.Move(_tempPath, _mainPath);
                }
            }
            catch (Exception exception) when (IsIoError(exception))
            {
                // The main file is untouched by a failed write; only the leftover temp file is cleaned up.
                _logger.Error("[Save] Failed to write the save to '" + _mainPath + "'; the previous save is kept: " + exception.Message);
                TryDeleteFile(_tempPath);
            }
        }

        /// <inheritdoc />
        public void Delete()
        {
            TryDeleteFile(_mainPath);
            TryDeleteFile(_backupPath);
            TryDeleteFile(_tempPath);
        }

        private void TryDeleteFile(string path)
        {
            try
            {
                // File.Delete does not throw for a missing file, so no Exists check is needed.
                File.Delete(path);
            }
            catch (Exception exception) when (IsIoError(exception))
            {
                _logger.Error("[Save] Failed to delete '" + path + "': " + exception.Message);
            }
        }

        // Why: only failures of the file system are swallowed; programming errors still surface.
        private static bool IsIoError(Exception exception)
        {
            return exception is IOException
                || exception is UnauthorizedAccessException
                || exception is SecurityException
                || exception is NotSupportedException;
        }
    }
}

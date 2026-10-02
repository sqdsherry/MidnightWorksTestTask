using System;
using System.Collections.Generic;
using System.Globalization;
using AutoService.Services.Settings;
using UnityEngine;

namespace AutoService.Presentation.Settings
{
    /// <summary>
    /// Drives the <see cref="SettingsView"/> in the main menu and in the pause menu: fills it from
    /// <see cref="ISettingsService.Current"/> on opening and applies every change at once.
    /// </summary>
    /// <remarks>
    /// Volumes are previewed while a slider is dragged and saved when it is released (or the screen closes), so dragging
    /// does not write to disk every frame. Every other control saves on change.
    /// <para>
    /// The quality and resolution lists are built on every opening (not in a tick): the monitor or the window may have
    /// changed since the last time.</para>
    /// </remarks>
    public sealed class SettingsPresenter : IDisposable
    {
        private readonly ISettingsService _settings;
        private readonly SettingsView _view;
        private readonly List<Vector2Int> _resolutions = new List<Vector2Int>();
        private readonly List<string> _options = new List<string>();
        private bool _volumeUnsaved;
        private bool _disposed;

        /// <summary>Creates the presenter, subscribes to the view and hides it.</summary>
        /// <param name="settings">The player's settings.</param>
        /// <param name="view">The settings screen of this scene.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public SettingsPresenter(ISettingsService settings, SettingsView view)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _view = view != null ? view : throw new ArgumentNullException(nameof(view));

            _view.MusicChanged += OnMusicChanged;
            _view.SfxChanged += OnSfxChanged;
            _view.VolumeReleased += CommitVolume;
            _view.QualityChanged += OnQualityChanged;
            _view.FullscreenChanged += OnFullscreenChanged;
            _view.ResolutionChanged += OnResolutionChanged;
            _view.BackClicked += Close;
            _view.Hide();
        }

        /// <summary>Raised when the screen closes (Back, Esc or <see cref="Close"/>).</summary>
        public event Action Closed;

        /// <summary>True while the screen is shown.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Fills the screen with the current settings and shows it.</summary>
        public void Open()
        {
            GameSettings current = _settings.Current;
            _view.SetVolumes(current.MusicVolume, current.SfxVolume);
            _view.SetFullscreen(current.Fullscreen);
            FillQuality(current.QualityLevel);
            FillResolutions(current);
            _view.Show();
            IsOpen = true;
        }

        /// <summary>Hides the screen and raises <see cref="Closed"/>. Does nothing when closed.</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            CommitVolume();
            if (_view != null)
            {
                _view.Hide();
            }

            Closed?.Invoke();
        }

        /// <summary>Unsubscribes from the view. Safe to call repeatedly.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Why: Unity's == — the view may already be destroyed while the scene unloads.
            if (_view != null)
            {
                _view.MusicChanged -= OnMusicChanged;
                _view.SfxChanged -= OnSfxChanged;
                _view.VolumeReleased -= CommitVolume;
                _view.QualityChanged -= OnQualityChanged;
                _view.FullscreenChanged -= OnFullscreenChanged;
                _view.ResolutionChanged -= OnResolutionChanged;
                _view.BackClicked -= Close;
            }

            CommitVolume();
            IsOpen = false;
        }

        private void FillQuality(int level)
        {
            _options.Clear();
            _options.AddRange(QualitySettings.names);
            int selected = _options.Count == 0 ? 0 : Mathf.Clamp(level, 0, _options.Count - 1);
            _view.SetQualityOptions(_options, selected);
        }

        /// <summary>
        /// The monitor's resolutions without the refresh-rate duplicates, largest first, with the current one selected.
        /// </summary>
        private void FillResolutions(GameSettings current)
        {
            _resolutions.Clear();
            Resolution[] available = Screen.resolutions;
            for (int i = 0; i < available.Length; i++)
            {
                AddUnique(new Vector2Int(available[i].width, available[i].height));
            }

            // Why: 0 x 0 in the settings means "the size the game runs at"; that size is shown even when the monitor does
            // not list it (a resized window), so the dropdown never claims a resolution that is not in effect.
            Vector2Int active = current.ResolutionWidth > 0
                ? new Vector2Int(current.ResolutionWidth, current.ResolutionHeight)
                : new Vector2Int(Screen.width, Screen.height);
            AddUnique(active);
            _resolutions.Sort(CompareLargestFirst);

            _options.Clear();
            string format = _view.ResolutionFormat;
            for (int i = 0; i < _resolutions.Count; i++)
            {
                _options.Add(string.Format(
                    format,
                    _resolutions[i].x.ToString(CultureInfo.InvariantCulture),
                    _resolutions[i].y.ToString(CultureInfo.InvariantCulture)));
            }

            _view.SetResolutionOptions(_options, Mathf.Max(0, _resolutions.IndexOf(active)));
        }

        private void AddUnique(Vector2Int resolution)
        {
            if (resolution.x > 0 && resolution.y > 0 && !_resolutions.Contains(resolution))
            {
                _resolutions.Add(resolution);
            }
        }

        private static int CompareLargestFirst(Vector2Int a, Vector2Int b)
        {
            int byWidth = b.x.CompareTo(a.x);
            return byWidth != 0 ? byWidth : b.y.CompareTo(a.y);
        }

        private void OnMusicChanged(float value)
        {
            _volumeUnsaved = true;
            _settings.Preview(_settings.Current.WithMusicVolume(value));
        }

        private void OnSfxChanged(float value)
        {
            _volumeUnsaved = true;
            _settings.Preview(_settings.Current.WithSfxVolume(value));
        }

        // Why: also on closing — a slider moved with the keyboard or released outside the screen has no pointer-up.
        private void CommitVolume()
        {
            if (!_volumeUnsaved)
            {
                return;
            }

            _volumeUnsaved = false;
            _settings.Set(_settings.Current);
        }

        private void OnQualityChanged(int index) => _settings.Set(_settings.Current.WithQualityLevel(index));

        private void OnFullscreenChanged(bool value) => _settings.Set(_settings.Current.WithFullscreen(value));

        private void OnResolutionChanged(int index)
        {
            if (index >= 0 && index < _resolutions.Count)
            {
                _settings.Set(_settings.Current.WithResolution(_resolutions[index].x, _resolutions[index].y));
            }
        }
    }
}

using System;
using System.Collections.Generic;
using AutoService.Presentation.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoService.Presentation.Settings
{
    /// <summary>
    /// Settings screen: music and SFX volume (with percent), quality, fullscreen, resolution and Back. Passive view:
    /// the presenter fills the values and option lists, every change goes out as an event at once (no Apply button).
    /// </summary>
    /// <remarks>
    /// Put it on the screen's root; showing and hiding switch that object. Labels live on the prefab; numbers are written
    /// with the non-allocating <c>TMP_Text.SetText</c>.
    /// </remarks>
    public sealed class SettingsView : MonoBehaviour
    {
        [SerializeField]
        private Slider _musicSlider;

        [SerializeField]
        private TMP_Text _musicValue;

        [SerializeField]
        private Slider _sfxSlider;

        [SerializeField]
        private TMP_Text _sfxValue;

        [SerializeField]
        private TMP_Dropdown _qualityDropdown;

        [SerializeField]
        private Toggle _fullscreenToggle;

        [SerializeField]
        private TMP_Dropdown _resolutionDropdown;

        [SerializeField]
        private Button _backButton;

        [SerializeField]
        [Tooltip("Volume label; {0} = percent (0..100).")]
        private string _percentFormat = "{0}%";

        [SerializeField]
        [Tooltip("Resolution option; {0} = width, {1} = height.")]
        private string _resolutionFormat = "{0} x {1}";

        /// <summary>Raised while the music slider moves; the argument is the volume in [0, 1].</summary>
        public event Action<float> MusicChanged;

        /// <summary>Raised while the SFX slider moves; the argument is the volume in [0, 1].</summary>
        public event Action<float> SfxChanged;

        /// <summary>Raised when another quality preset is picked; the argument is its index in the list.</summary>
        public event Action<int> QualityChanged;

        /// <summary>Raised when the fullscreen toggle flips; the argument is the new value.</summary>
        public event Action<bool> FullscreenChanged;

        /// <summary>Raised when another resolution is picked; the argument is its index in the list.</summary>
        public event Action<int> ResolutionChanged;

        /// <summary>Raised when Back is clicked.</summary>
        public event Action BackClicked;

        /// <summary>Resolution option format; {0} = width, {1} = height.</summary>
        public string ResolutionFormat => _resolutionFormat;

        // Why: Awake runs once, on the first activation, so the listeners are never doubled.
        private void Awake()
        {
            if (_musicSlider != null)
            {
                _musicSlider.onValueChanged.AddListener(OnMusicSlider);
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.onValueChanged.AddListener(OnSfxSlider);
            }

            if (_qualityDropdown != null)
            {
                _qualityDropdown.onValueChanged.AddListener(OnQualityPicked);
            }

            if (_fullscreenToggle != null)
            {
                _fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
            }

            if (_resolutionDropdown != null)
            {
                _resolutionDropdown.onValueChanged.AddListener(OnResolutionPicked);
            }

            if (_backButton != null)
            {
                _backButton.onClick.AddListener(OnBack);
            }
        }

        private void OnDestroy()
        {
            if (_musicSlider != null)
            {
                _musicSlider.onValueChanged.RemoveListener(OnMusicSlider);
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.onValueChanged.RemoveListener(OnSfxSlider);
            }

            if (_qualityDropdown != null)
            {
                _qualityDropdown.onValueChanged.RemoveListener(OnQualityPicked);
            }

            if (_fullscreenToggle != null)
            {
                _fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenToggled);
            }

            if (_resolutionDropdown != null)
            {
                _resolutionDropdown.onValueChanged.RemoveListener(OnResolutionPicked);
            }

            if (_backButton != null)
            {
                _backButton.onClick.RemoveListener(OnBack);
            }
        }

        /// <summary>Shows the screen.</summary>
        public void Show() => UiVisibility.ShowChain(gameObject);

        /// <summary>Hides the screen.</summary>
        /// <remarks>
        /// Why no explicit dropdown Hide(): a dropdown destroys its open list in its own OnDisable, which deactivating the
        /// screen triggers; calling Hide() would also select the dropdown in the EventSystem.
        /// </remarks>
        public void Hide() => UiVisibility.Hide(gameObject);

        /// <summary>Shows the volumes without raising change events.</summary>
        public void SetVolumes(float music, float sfx)
        {
            if (_musicSlider != null)
            {
                _musicSlider.SetValueWithoutNotify(music);
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.SetValueWithoutNotify(sfx);
            }

            ShowPercent(_musicValue, music);
            ShowPercent(_sfxValue, sfx);
        }

        /// <summary>Shows the fullscreen flag without raising a change event.</summary>
        public void SetFullscreen(bool fullscreen)
        {
            if (_fullscreenToggle != null)
            {
                _fullscreenToggle.SetIsOnWithoutNotify(fullscreen);
            }
        }

        /// <summary>Replaces the quality list and selects <paramref name="selected"/> without raising a change event.</summary>
        public void SetQualityOptions(List<string> options, int selected) => SetOptions(_qualityDropdown, options, selected);

        /// <summary>Replaces the resolution list and selects <paramref name="selected"/> without raising a change event.</summary>
        public void SetResolutionOptions(List<string> options, int selected) => SetOptions(_resolutionDropdown, options, selected);

        private static void SetOptions(TMP_Dropdown dropdown, List<string> options, int selected)
        {
            if (dropdown == null)
            {
                return;
            }

            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(selected);
            dropdown.RefreshShownValue();
        }

        private void ShowPercent(TMP_Text label, float volume)
        {
            if (label != null)
            {
                label.SetText(_percentFormat, Mathf.Round(volume * 100f));
            }
        }

        private void OnMusicSlider(float value)
        {
            ShowPercent(_musicValue, value);
            MusicChanged?.Invoke(value);
        }

        private void OnSfxSlider(float value)
        {
            ShowPercent(_sfxValue, value);
            SfxChanged?.Invoke(value);
        }

        private void OnQualityPicked(int index) => QualityChanged?.Invoke(index);

        private void OnFullscreenToggled(bool value) => FullscreenChanged?.Invoke(value);

        private void OnResolutionPicked(int index) => ResolutionChanged?.Invoke(index);

        private void OnBack() => BackClicked?.Invoke();
    }
}

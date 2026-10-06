using System;
using AutoService.Services.Events;
using UnityEngine;
using UnityEngine.UI;

namespace AutoService.Infrastructure.Services.Audio
{
    /// <summary>
    /// Implementation of IAudioService using Unity's AudioSource.
    /// Manages SFX, music playback, and auto-wires button click sounds.
    /// </summary>
    public class AudioService : MonoBehaviour, IAudioService, IDisposable
    {
        [SerializeField] private AudioSource _sfxSource;
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioClip _cashClip;
        [SerializeField] private AudioClip _workCompletedClip;
        [SerializeField] private AudioClip _buttonClickClip;
        [SerializeField] private AudioClip _levelUpClip;

        private static AudioService _instance;
        public static AudioService Instance => _instance;

        private IEventBus _eventBus;
        private Action<AutoService.Services.Economy.BalanceChangedEvent> _onBalanceChanged;
        private Action<AutoService.Services.Points.ServiceCompletedEvent> _onServiceCompleted;

        private float _masterVolume = 1f;
        private float _sfxVolume = 1f;
        private float _musicVolume = 1f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureAudioSources();
            LoadFallbackClips();
        }

        private void Start()
        {
            RegisterAllSceneButtons();
        }

        private void EnsureAudioSources()
        {
            if (_sfxSource == null)
            {
                var sfxGo = transform.Find("SfxSource");
                if (sfxGo != null) _sfxSource = sfxGo.GetComponent<AudioSource>();
                if (_sfxSource == null) _sfxSource = gameObject.AddComponent<AudioSource>();
            }

            if (_musicSource == null)
            {
                _musicSource = gameObject.GetComponent<AudioSource>();
                if (_musicSource == null) _musicSource = gameObject.AddComponent<AudioSource>();
            }

            _sfxSource.playOnAwake = false;
            _sfxSource.volume = _masterVolume * _sfxVolume;

            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            _musicSource.volume = _masterVolume * _musicVolume;
        }

        private void LoadFallbackClips()
        {
            if (_buttonClickClip == null) _buttonClickClip = Resources.Load<AudioClip>("Audio/sfx_click");
            if (_cashClip == null) _cashClip = Resources.Load<AudioClip>("Audio/sfx_money");
            if (_workCompletedClip == null) _workCompletedClip = Resources.Load<AudioClip>("Audio/sfx_service_complete");
            if (_levelUpClip == null) _levelUpClip = Resources.Load<AudioClip>("Audio/sfx_level_up");
        }

        public void RegisterAllSceneButtons()
        {
            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in buttons)
            {
                b.onClick.RemoveListener(OnButtonClicked);
                b.onClick.AddListener(OnButtonClicked);
            }
        }

        public void ApplyVolumes(float master, float sfx, float music)
        {
            _masterVolume = Mathf.Clamp01(master);
            _sfxVolume = Mathf.Clamp01(sfx);
            _musicVolume = Mathf.Clamp01(music);

            if (_sfxSource != null) _sfxSource.volume = _masterVolume * _sfxVolume;
            if (_musicSource != null) _musicSource.volume = _masterVolume * _musicVolume;
        }

        public void Initialize(IEventBus eventBus)
        {
            _eventBus = eventBus;

            _onBalanceChanged = OnBalanceChanged;
            _onServiceCompleted = OnServiceCompleted;

            if (_eventBus != null)
            {
                _eventBus.Subscribe(_onBalanceChanged);
                _eventBus.Subscribe(_onServiceCompleted);
            }

            UiEvents.OnAnyButtonClicked += OnButtonClicked;
            RegisterAllSceneButtons();
        }

        public void Dispose()
        {
            if (_eventBus != null)
            {
                if (_onBalanceChanged != null) _eventBus.Unsubscribe(_onBalanceChanged);
                if (_onServiceCompleted != null) _eventBus.Unsubscribe(_onServiceCompleted);
            }

            UiEvents.OnAnyButtonClicked -= OnButtonClicked;
        }

        private void OnButtonClicked()
        {
            PlaySfx(_buttonClickClip, "Button Click");
        }

        private void OnBalanceChanged(AutoService.Services.Economy.BalanceChangedEvent evt)
        {
            if (evt.Delta > 0)
            {
                PlaySfx(_cashClip, "Cash");
            }
        }

        private void OnServiceCompleted(AutoService.Services.Points.ServiceCompletedEvent evt)
        {
            PlaySfx(_workCompletedClip, "Work Completed");
        }

        public void PlaySfx(AudioClip clip)
        {
            PlaySfx(clip, clip != null ? clip.name : "Unknown");
        }

        private void PlaySfx(AudioClip clip, string fallbackName)
        {
            EnsureAudioSources();

            if (clip != null)
            {
                if (_sfxSource != null)
                {
                    _sfxSource.PlayOneShot(clip, _masterVolume * _sfxVolume);
                }
            }
            else
            {
                Debug.Log($"[AudioService] Playing SFX: {fallbackName} (AudioClip missing)");
            }
        }

        public void PlayMusic(AudioClip clip)
        {
            EnsureAudioSources();

            if (clip != null && _musicSource != null)
            {
                if (_musicSource.clip != clip)
                {
                    _musicSource.clip = clip;
                    _musicSource.Play();
                }
            }
        }
    }
}

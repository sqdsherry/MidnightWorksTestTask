using System;
using AutoService.Services.Events;
using UnityEngine;

namespace AutoService.Infrastructure.Services.Audio
{
    /// <summary>
    /// Implementation of IAudioService using Unity's AudioSource.
    /// Acts as a stub since actual clips might not be assigned yet.
    /// </summary>
    public class AudioService : MonoBehaviour, IAudioService, IDisposable
    {
        [SerializeField] private AudioSource _sfxSource;
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioClip _cashClip;
        [SerializeField] private AudioClip _workCompletedClip;

        [SerializeField] private AudioClip _buttonClickClip;

        private IEventBus _eventBus;
        private Action<AutoService.Services.Economy.BalanceChangedEvent> _onBalanceChanged;
        private Action<AutoService.Services.Points.ServiceCompletedEvent> _onServiceCompleted;

        public void Initialize(IEventBus eventBus)
        {
            _eventBus = eventBus;
            
            _onBalanceChanged = OnBalanceChanged;
            _onServiceCompleted = OnServiceCompleted;

            _eventBus.Subscribe(_onBalanceChanged);
            _eventBus.Subscribe(_onServiceCompleted);

            AutoService.Presentation.Ui.ButtonAnimator.OnAnyButtonClicked += OnButtonClicked;
        }

        public void Dispose()
        {
            if (_eventBus != null)
            {
                if (_onBalanceChanged != null) _eventBus.Unsubscribe(_onBalanceChanged);
                if (_onServiceCompleted != null) _eventBus.Unsubscribe(_onServiceCompleted);
            }
            
            AutoService.Presentation.Ui.ButtonAnimator.OnAnyButtonClicked -= OnButtonClicked;
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
            if (clip != null)
            {
                if (_sfxSource != null)
                {
                    _sfxSource.PlayOneShot(clip);
                }
                else
                {
                    Debug.Log($"[AudioService] Playing SFX: {clip.name}");
                }
            }
            else
            {
                Debug.Log($"[AudioService] Playing SFX: {fallbackName} (AudioClip is missing)");
            }
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip != null)
            {
                if (_musicSource != null)
                {
                    if (_musicSource.clip != clip)
                    {
                        _musicSource.clip = clip;
                        _musicSource.Play();
                    }
                }
                else
                {
                    Debug.Log($"[AudioService] Playing Music: {clip.name}");
                }
            }
        }
    }
}

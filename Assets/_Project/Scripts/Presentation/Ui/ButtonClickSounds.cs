using System;
using System.Collections.Generic;
using AutoService.Services.Audio;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AutoService.Presentation.Ui
{
    /// <summary>
    /// Adds the click sound to every <see cref="Button"/> under the given roots (inactive ones included).
    /// </summary>
    /// <remarks>
    /// Bind once all UI of the scene exists (runtime-instantiated popups included); buttons created later are not covered.
    /// </remarks>
    public sealed class ButtonClickSounds : IDisposable
    {
        private readonly IAudioService _audio;
        private readonly UnityAction _onClick;
        private readonly List<Button> _buttons = new List<Button>();

        /// <summary>Creates the binder; nothing is subscribed until <see cref="Bind"/>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="audio"/> is null.</exception>
        public ButtonClickSounds(IAudioService audio)
        {
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _onClick = OnClick;
        }

        /// <summary>Subscribes every button found under <paramref name="roots"/>; already bound buttons are skipped.</summary>
        public void Bind(GameObject[] roots)
        {
            if (roots == null)
            {
                return;
            }

            for (int r = 0; r < roots.Length; r++)
            {
                Button[] buttons = roots[r].GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (_buttons.Contains(buttons[i]))
                    {
                        continue;
                    }

                    buttons[i].onClick.AddListener(_onClick);
                    _buttons.Add(buttons[i]);
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                // Why: Unity's == — buttons of an unloading scene may already be destroyed.
                if (_buttons[i] != null)
                {
                    _buttons[i].onClick.RemoveListener(_onClick);
                }
            }

            _buttons.Clear();
        }

        private void OnClick() => _audio.PlaySfx(SfxKind.ButtonClick);
    }
}

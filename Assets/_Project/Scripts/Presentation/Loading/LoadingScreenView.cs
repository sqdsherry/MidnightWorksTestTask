using System.Collections;
using System.Collections.Generic;
using AutoService.Services.Scenes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoService.Presentation.Loading
{
    /// <summary>
    /// Full-screen loading screen: game title, progress bar and a tip. Lives in the Boot scene under the project
    /// composition root, so it survives every scene load. Passive view, driven by <see cref="LoadingScreenPresenter"/>.
    /// </summary>
    /// <remarks>
    /// Fades in and out through a <see cref="CanvasGroup"/> on unscaled time (a paused game must not freeze it). While
    /// hidden only the canvas is disabled — this object stays active so the fade coroutine can run.
    /// <para>As the <see cref="ILoadingCurtain"/> it tells the scene loader when the fade-in has finished.</para>
    /// </remarks>
    public sealed class LoadingScreenView : MonoBehaviour, ILoadingCurtain
    {
        [SerializeField]
        [Tooltip("Own canvas of the loading screen (sorting order above every scene UI).")]
        private Canvas _canvas;

        [SerializeField]
        [Tooltip("Fades the whole screen.")]
        private CanvasGroup _group;

        [SerializeField]
        [Tooltip("Progress bar: Image of type Filled, Horizontal.")]
        private Image _progressFill;

        [SerializeField]
        private TMP_Text _tipLabel;

        [SerializeField, Min(0f)]
        [Tooltip("Fade in/out duration, in real seconds.")]
        private float _fadeSeconds = 0.2f;

        [SerializeField]
        [Tooltip("One of these is shown on every load.")]
        private string[] _tips =
        {
            "Hire a worker to automate a bay.",
            "Blue pads open upgrades and hiring.",
            "Storekeepers carry boxes for you.",
            "A bay without supplies stops working - bring a box from the warehouse.",
            "Cars that wait too long drive away angry.",
            "Upgrade the price to earn more from every car.",
            "Click the barrier to let the next car in.",
        };

        private Coroutine _fade;

        /// <summary>Tips to pick from; never null.</summary>
        public IReadOnlyList<string> Tips => _tips ?? System.Array.Empty<string>();

        /// <inheritdoc />
        public bool IsOpaque => (_canvas == null || _canvas.enabled) && (_group == null || _group.alpha >= 1f);

        private void Awake()
        {
            SetVisibleNow(false);
        }

        private void OnDisable()
        {
            // Why: Unity stops the coroutines of a disabled object; the field must not point at a dead one.
            _fade = null;
        }

        /// <summary>Shows the screen with an empty bar and <paramref name="tip"/>.</summary>
        public void Show(string tip)
        {
            if (_tipLabel != null)
            {
                _tipLabel.text = tip ?? string.Empty;
            }

            SetProgress(0f);
            if (_canvas != null)
            {
                _canvas.enabled = true;
            }

            if (_group != null)
            {
                // Why: blocks clicks on the scenes behind at once, not after the fade.
                _group.blocksRaycasts = true;
            }

            FadeTo(1f);
        }

        /// <summary>Fills the bar to <paramref name="progress"/> (clamped to [0, 1]).</summary>
        public void SetProgress(float progress)
        {
            if (_progressFill != null)
            {
                _progressFill.fillAmount = Mathf.Clamp01(progress);
            }
        }

        /// <summary>Fades the screen out.</summary>
        public void Hide()
        {
            if (_group != null)
            {
                _group.blocksRaycasts = false;
            }

            FadeTo(0f);
        }

        private void FadeTo(float alpha)
        {
            if (_fade != null)
            {
                StopCoroutine(_fade);
                _fade = null;
            }

            if (_group == null || _fadeSeconds <= 0f || !isActiveAndEnabled)
            {
                SetVisibleNow(alpha > 0f);
                return;
            }

            _fade = StartCoroutine(Fade(alpha));
        }

        private IEnumerator Fade(float target)
        {
            float start = _group.alpha;
            float speed = 1f / _fadeSeconds;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * speed)
            {
                _group.alpha = Mathf.Lerp(start, target, t);
                yield return null;
            }

            _fade = null;
            SetVisibleNow(target > 0f);
        }

        private void SetVisibleNow(bool visible)
        {
            if (_group != null)
            {
                _group.alpha = visible ? 1f : 0f;
                _group.blocksRaycasts = visible;
            }

            if (_canvas != null)
            {
                _canvas.enabled = visible;
            }
        }
    }
}

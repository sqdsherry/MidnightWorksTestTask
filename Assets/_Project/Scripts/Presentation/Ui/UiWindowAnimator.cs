using System;
using System.Collections;
using UnityEngine;

namespace AutoService.Presentation.Ui
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UiWindowAnimator : MonoBehaviour
    {
        [SerializeField] private AnimationCurve _scaleCurveIn = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField] private AnimationCurve _fadeCurveIn = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField] private AnimationCurve _scaleCurveOut = AnimationCurve.Linear(0, 1, 1, 0);
        [SerializeField] private AnimationCurve _fadeCurveOut = AnimationCurve.Linear(0, 1, 1, 0);
        [SerializeField] private float _duration = 0.3f;

        private CanvasGroup _canvasGroup;
        private Coroutine _currentAnim;
        private Vector3 _originalScale;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _originalScale = transform.localScale;
            
            // Setup default Overshoot curve if not configured
            if (_scaleCurveIn.keys.Length <= 2)
            {
                _scaleCurveIn = new AnimationCurve(
                    new Keyframe(0, 0.8f), 
                    new Keyframe(0.7f, 1.05f), 
                    new Keyframe(1f, 1f)
                );
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (_currentAnim != null) StopCoroutine(_currentAnim);
            _currentAnim = StartCoroutine(AnimateRoutine(_scaleCurveIn, _fadeCurveIn, () => {}));
        }

        public void Hide(Action onComplete = null)
        {
            if (_currentAnim != null) StopCoroutine(_currentAnim);
            _currentAnim = StartCoroutine(AnimateRoutine(_scaleCurveOut, _fadeCurveOut, () =>
            {
                gameObject.SetActive(false);
                onComplete?.Invoke();
            }));
        }

        private IEnumerator AnimateRoutine(AnimationCurve scaleCurve, AnimationCurve fadeCurve, Action onComplete)
        {
            float time = 0f;
            while (time < _duration)
            {
                float t = time / _duration;
                transform.localScale = _originalScale * scaleCurve.Evaluate(t);
                _canvasGroup.alpha = fadeCurve.Evaluate(t);

                time += Time.unscaledDeltaTime;
                yield return null;
            }

            transform.localScale = _originalScale * scaleCurve.Evaluate(1f);
            _canvasGroup.alpha = fadeCurve.Evaluate(1f);
            
            onComplete?.Invoke();
        }
    }
}

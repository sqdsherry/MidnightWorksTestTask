using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace AutoService.Presentation.Ui.FloatingText
{
    public class FloatingText : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private AnimationCurve _yMoveCurve;
        [SerializeField] private AnimationCurve _alphaCurve;
        [SerializeField] private float _duration = 1.5f;

        private Action<FloatingText> _onComplete;
        private Coroutine _animationCoroutine;

        private void Awake()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            if (_yMoveCurve == null || _yMoveCurve.length == 0) _yMoveCurve = AnimationCurve.Linear(0, 0, 1, 1);
            if (_alphaCurve == null || _alphaCurve.length == 0) _alphaCurve = AnimationCurve.Linear(0, 1, 1, 0);
        }

        public void Play(string textContent, Vector3 startPos, Action<FloatingText> onComplete)
        {
            _text.text = textContent;
            transform.position = startPos;
            _onComplete = onComplete;

            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }
            _animationCoroutine = StartCoroutine(AnimateRoutine(startPos));
        }

        private IEnumerator AnimateRoutine(Vector3 startPos)
        {
            float time = 0f;
            while (time < _duration)
            {
                float t = time / _duration;
                
                float yOffset = _yMoveCurve.Evaluate(t);
                transform.position = startPos + new Vector3(0, yOffset, 0);

                float alpha = _alphaCurve.Evaluate(t);
                Color c = _text.color;
                c.a = alpha;
                _text.color = c;

                time += Time.deltaTime;
                yield return null;
            }

            _onComplete?.Invoke(this);
        }
    }
}

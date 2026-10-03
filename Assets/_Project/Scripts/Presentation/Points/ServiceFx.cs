using AutoService.Domain.Points;
using UnityEngine;

namespace AutoService.Presentation.Points
{
    public abstract class ServiceFx : MonoBehaviour
    {
        public abstract void Render(ServicePointState state, float progress, float dt);
    }

    public sealed class WashFx : ServiceFx
    {
        [SerializeField] private Transform _leftBrush;
        [SerializeField] private Transform _rightBrush;
        [SerializeField] private ParticleSystem _foam;

        public override void Render(ServicePointState state, float progress, float dt)
        {
            bool isServicing = state == ServicePointState.Servicing;
            
            if (_foam != null)
            {
                var emission = _foam.emission;
                emission.enabled = isServicing;
            }

            if (isServicing)
            {
                if (_leftBrush != null)
                {
                    _leftBrush.Rotate(0f, 360f * dt, 0f, Space.Self);
                    _leftBrush.localPosition = new Vector3(-1.5f + Mathf.Sin(Time.time * 2f) * 0.2f, _leftBrush.localPosition.y, _leftBrush.localPosition.z);
                }
                if (_rightBrush != null)
                {
                    _rightBrush.Rotate(0f, -360f * dt, 0f, Space.Self);
                    _rightBrush.localPosition = new Vector3(1.5f - Mathf.Sin(Time.time * 2f) * 0.2f, _rightBrush.localPosition.y, _rightBrush.localPosition.z);
                }
            }
        }
    }

    public sealed class LiftFx : ServiceFx
    {
        [SerializeField] private Transform[] _plates;

        public override void Render(ServicePointState state, float progress, float dt)
        {
            float height = 0f;
            if (state == ServicePointState.Servicing)
            {
                if (progress <= 0.15f)
                    height = Mathf.Lerp(0f, 0.8f, progress / 0.15f);
                else if (progress >= 0.85f)
                    height = Mathf.Lerp(0.8f, 0f, (progress - 0.85f) / 0.15f);
                else
                    height = 0.8f;
            }

            if (_plates != null)
            {
                for (int i = 0; i < _plates.Length; i++)
                {
                    if (_plates[i] != null)
                    {
                        var pos = _plates[i].localPosition;
                        pos.y = height;
                        _plates[i].localPosition = pos;
                    }
                }
            }
        }
    }

    public sealed class TireFx : ServiceFx
    {
        [SerializeField] private Transform _tires;
        [SerializeField] private Transform _robotArm;
        [SerializeField] private AnimationCurve _jumpCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private float _time;

        public override void Render(ServicePointState state, float progress, float dt)
        {
            if (state == ServicePointState.Servicing)
            {
                _time += dt;
                
                if (_tires != null)
                {
                    float jumpPhase = (_time % 1.5f) / 1.5f;
                    float height = _jumpCurve.Evaluate(jumpPhase) * 0.5f;
                    _tires.localPosition = new Vector3(_tires.localPosition.x, height, _tires.localPosition.z);
                }

                if (_robotArm != null)
                {
                    float angle = Mathf.Sin(_time * 3f) * 30f;
                    _robotArm.localRotation = Quaternion.Euler(0f, angle, 0f);
                }
            }
            else
            {
                _time = 0f;
                if (_tires != null) _tires.localPosition = new Vector3(_tires.localPosition.x, 0f, _tires.localPosition.z);
                if (_robotArm != null) _robotArm.localRotation = Quaternion.identity;
            }
        }
    }
}

using AutoService.Domain.Points;
using UnityEngine;

namespace AutoService.Presentation.Points
{
    /// <summary>
    /// Visual effects for the car wash service bay: spinning rotating brushes and soap foam particles.
    /// </summary>
    public sealed class WashFx : ServiceFx
    {
        [SerializeField]
        [Tooltip("Left vertical brush transform.")]
        private Transform _leftBrush;

        [SerializeField]
        [Tooltip("Right vertical brush transform.")]
        private Transform _rightBrush;

        [SerializeField]
        [Tooltip("Soap foam particle system.")]
        private ParticleSystem _foam;

        [SerializeField]
        [Tooltip("Optional foam material override.")]
        private Material _foamMaterial;

        private ParticleSystemRenderer _foamRenderer;

        private void Awake()
        {
            EnsureFoamParticles();
        }

        private void EnsureFoamParticles()
        {
            if (_foam == null)
            {
                _foam = GetComponentInChildren<ParticleSystem>();
            }

            if (_foam == null)
            {
                return;
            }

            _foamRenderer = _foam.GetComponent<ParticleSystemRenderer>();
            if (_foamRenderer != null)
            {
                if (_foamMaterial != null)
                {
                    _foamRenderer.sharedMaterial = _foamMaterial;
                }
                else if (_foamRenderer.sharedMaterial == null ||
                         _foamRenderer.sharedMaterial.shader == null ||
                         _foamRenderer.sharedMaterial.shader.name.Contains("Error") ||
                         _foamRenderer.sharedMaterial.shader.name.Contains("Standard Unlit"))
                {
                    Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                    if (particleShader == null)
                    {
                        particleShader = Shader.Find("Universal Render Pipeline/Unlit");
                    }

                    if (particleShader != null)
                    {
                        var mat = new Material(particleShader);
                        mat.name = "M_RuntimeWashFoam";
                        mat.color = new Color(0.92f, 0.96f, 1f, 0.85f);
                        _foamRenderer.material = mat;
                    }
                }
            }

            var main = _foam.main;
            main.useUnscaledTime = false;
            main.startColor = new Color(0.92f, 0.96f, 1f, 0.85f);
            main.startLifetime = 1.0f;
            main.startSpeed = 1.8f;
            main.startSize = 0.35f;
            main.gravityModifier = 0.8f;

            var emission = _foam.emission;
            emission.rateOverTime = 40f;
            emission.enabled = false;

            var shape = _foam.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(3f, 0.5f, 3f);
        }

        /// <inheritdoc />
        public override void Render(ServicePointState state, float progress, float dt)
        {
            bool isServicing = state == ServicePointState.Servicing;

            if (_foam != null)
            {
                var emission = _foam.emission;
                if (emission.enabled != isServicing)
                {
                    emission.enabled = isServicing;
                }
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
}

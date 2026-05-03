using System;
using UnityEngine;

namespace LittlePlanet.Particles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem), typeof(ParticleSystemRenderer))]
    public sealed class UniversalParticleEffect : MonoBehaviour
    {
        [SerializeField] private UniversalParticlePreset preset;
        [SerializeField, Min(0f)] private float intensity = 1f;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private Material fallbackMaterial;

        private ParticleSystem _particleSystem;
        private Material _runtimeFallbackMaterial;

        public event Action<UniversalParticleEffect> Stopped;

        public UniversalParticlePreset Preset => preset;
        public float Intensity => intensity;
        public ParticleSystem ParticleSystem
        {
            get
            {
                ResolveReferences();
                return _particleSystem;
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyPreset();
        }

        private void OnEnable()
        {
            ApplyPreset();
            if (playOnEnable)
            {
                Play();
            }
        }

        private void OnDestroy()
        {
            if (_runtimeFallbackMaterial != null)
            {
                Destroy(_runtimeFallbackMaterial);
            }
        }

        private void OnValidate()
        {
            intensity = Mathf.Max(0f, intensity);
            ResolveReferences();
            ApplyPreset();
        }

        private void OnParticleSystemStopped()
        {
            Stopped?.Invoke(this);
        }

        public void Configure(UniversalParticlePreset nextPreset, float nextIntensity = 1f, bool restart = true)
        {
            preset = nextPreset;
            intensity = Mathf.Max(0f, nextIntensity);
            ApplyPreset();
            if (restart)
            {
                Play();
            }
        }

        public void SetIntensity(float nextIntensity, bool restart = false)
        {
            intensity = Mathf.Max(0f, nextIntensity);
            ApplyPreset();
            if (restart)
            {
                Play();
            }
        }

        public void ApplyPreset()
        {
            ResolveReferences();
            if (preset == null || _particleSystem == null)
            {
                return;
            }

            StopBeforeReconfigure();
            preset.ApplyTo(_particleSystem, intensity, GetFallbackMaterial());
        }

        public void Play()
        {
            ResolveReferences();
            if (_particleSystem == null)
            {
                return;
            }

            ApplyPreset();
            _particleSystem.Clear(true);
            _particleSystem.Play(true);
        }

        public void Stop(bool clear = false)
        {
            ResolveReferences();
            if (_particleSystem == null)
            {
                return;
            }

            _particleSystem.Stop(true, clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting);
        }

        public void PrepareForPool()
        {
            ResolveReferences();
            if (_particleSystem != null)
            {
                _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _particleSystem.Clear(true);
            }
        }

        private void ResolveReferences()
        {
            if (_particleSystem == null)
            {
                _particleSystem = GetComponent<ParticleSystem>();
            }
        }

        private void StopBeforeReconfigure()
        {
            if (_particleSystem == null || !_particleSystem.isPlaying)
            {
                return;
            }

            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _particleSystem.Clear(true);
        }

        private Material GetFallbackMaterial()
        {
            if (fallbackMaterial != null)
            {
                return fallbackMaterial;
            }

            if (_runtimeFallbackMaterial != null)
            {
                return _runtimeFallbackMaterial;
            }

            var shader = Shader.Find("LittlePlanet/UniversalParticleUnlit")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Standard");

            if (shader == null)
            {
                return null;
            }

            _runtimeFallbackMaterial = new Material(shader)
            {
                name = "UniversalParticleFallback_Runtime"
            };
            return _runtimeFallbackMaterial;
        }
    }
}

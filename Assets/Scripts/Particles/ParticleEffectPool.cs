using System.Collections.Generic;
using UnityEngine;

namespace LittlePlanet.Particles
{
    public sealed class ParticleEffectPool : MonoBehaviour
    {
        [SerializeField] private UniversalParticleEffect prefab;
        [SerializeField, Min(0)] private int prewarmCount = 4;
        [SerializeField, Min(1)] private int maxInstances = 16;
        [SerializeField] private bool parentActiveInstancesToPool;

        private readonly Queue<UniversalParticleEffect> _available = new();
        private readonly HashSet<UniversalParticleEffect> _active = new();
        private readonly List<UniversalParticleEffect> _all = new();

        private void Awake()
        {
            Prewarm();
        }

        public UniversalParticleEffect Spawn(
            UniversalParticlePreset preset,
            Vector3 position,
            Quaternion rotation,
            float intensity = 1f,
            Transform parent = null)
        {
            var effect = GetOrCreate();
            if (effect == null)
            {
                return null;
            }

            effect.transform.SetParent(parentActiveInstancesToPool ? transform : parent, worldPositionStays: false);
            effect.transform.SetPositionAndRotation(position, rotation);
            effect.gameObject.SetActive(true);
            effect.Configure(preset, intensity, restart: true);
            _active.Add(effect);
            return effect;
        }

        public void StopAll(bool clear = false)
        {
            var activeSnapshot = ListPool;
            activeSnapshot.Clear();
            activeSnapshot.AddRange(_active);
            for (var i = 0; i < activeSnapshot.Count; i++)
            {
                var effect = activeSnapshot[i];
                if (effect != null)
                {
                    effect.Stop(clear);
                }
            }

            activeSnapshot.Clear();
        }

        private void Prewarm()
        {
            for (var i = 0; i < prewarmCount; i++)
            {
                var effect = CreateInstance();
                if (effect == null)
                {
                    return;
                }

                ReturnToPool(effect);
            }
        }

        private UniversalParticleEffect GetOrCreate()
        {
            while (_available.Count > 0)
            {
                var effect = _available.Dequeue();
                if (effect != null)
                {
                    return effect;
                }
            }

            return _all.Count < maxInstances ? CreateInstance() : null;
        }

        private UniversalParticleEffect CreateInstance()
        {
            if (prefab == null)
            {
                return null;
            }

            var instance = Instantiate(prefab, transform);
            instance.gameObject.SetActive(false);
            instance.Stopped += HandleEffectStopped;
            _all.Add(instance);
            return instance;
        }

        private void HandleEffectStopped(UniversalParticleEffect effect)
        {
            ReturnToPool(effect);
        }

        private void ReturnToPool(UniversalParticleEffect effect)
        {
            if (effect == null)
            {
                return;
            }

            _active.Remove(effect);
            effect.PrepareForPool();
            effect.transform.SetParent(transform, worldPositionStays: false);
            effect.gameObject.SetActive(false);
            if (!_available.Contains(effect))
            {
                _available.Enqueue(effect);
            }
        }

        private static readonly List<UniversalParticleEffect> ListPool = new();
    }
}

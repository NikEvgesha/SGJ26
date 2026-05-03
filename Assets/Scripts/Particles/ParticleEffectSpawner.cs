using UnityEngine;

namespace LittlePlanet.Particles
{
    public sealed class ParticleEffectSpawner : MonoBehaviour
    {
        [SerializeField] private ParticleEffectPool pool;
        [SerializeField] private UniversalParticlePreset defaultPreset;
        [SerializeField, Min(0f)] private float defaultIntensity = 1f;

        private void Awake()
        {
            ResolveReferences();
        }

        public UniversalParticleEffect PlayAtSelf()
        {
            return Play(defaultPreset, transform.position, transform.rotation, defaultIntensity, null);
        }

        public UniversalParticleEffect PlayAtTransform(Transform target)
        {
            if (target == null)
            {
                return null;
            }

            return Play(defaultPreset, target.position, target.rotation, defaultIntensity, null);
        }

        public UniversalParticleEffect Play(
            UniversalParticlePreset preset,
            Vector3 position,
            Quaternion rotation,
            float intensity = 1f,
            Transform parent = null)
        {
            ResolveReferences();
            if (pool == null || preset == null)
            {
                return null;
            }

            return pool.Spawn(preset, position, rotation, intensity, parent);
        }

        private void ResolveReferences()
        {
            if (pool == null)
            {
                pool = FindFirstObjectByType<ParticleEffectPool>(FindObjectsInactive.Include);
            }
        }
    }
}

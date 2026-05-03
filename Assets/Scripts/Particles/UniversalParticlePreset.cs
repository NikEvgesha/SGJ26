using UnityEngine;

namespace LittlePlanet.Particles
{
    [CreateAssetMenu(menuName = "Little Planet/Particles/Universal Particle Preset", fileName = "ParticlePreset")]
    public sealed class UniversalParticlePreset : ScriptableObject
    {
        public enum ShapePreset
        {
            Cone,
            Sphere,
            Hemisphere,
            Circle
        }

        [Header("Budget")]
        [SerializeField, Min(1)] private int maxParticles = 300;
        [SerializeField, Min(0.05f)] private float duration = 2f;
        [SerializeField] private bool looping = true;
        [SerializeField] private bool prewarm;

        [Header("Emission")]
        [SerializeField, Min(0f)] private float emissionRate = 40f;
        [SerializeField, Min(0f)] private int burstCount;

        [Header("Particle")]
        [SerializeField] private Vector2 lifetime = new(0.8f, 1.6f);
        [SerializeField] private Vector2 speed = new(0.2f, 1.2f);
        [SerializeField] private Vector2 size = new(0.08f, 0.35f);
        [SerializeField, Range(0f, 1f)] private float randomRotation = 1f;
        [SerializeField] private float gravityModifier;
        [SerializeField] private Gradient colorOverLifetime = CreateDefaultGradient(Color.white);

        [Header("Shape")]
        [SerializeField] private ShapePreset shape = ShapePreset.Cone;
        [SerializeField, Min(0f)] private float shapeRadius = 0.25f;
        [SerializeField, Range(0f, 90f)] private float coneAngle = 25f;

        [Header("Noise")]
        [SerializeField] private bool noiseEnabled = true;
        [SerializeField, Min(0f)] private float noiseStrength = 0.35f;
        [SerializeField, Min(0.01f)] private float noiseFrequency = 0.5f;
        [SerializeField, Min(0f)] private float noiseScrollSpeed = 0.25f;

        [Header("Renderer")]
        [SerializeField] private Material material;
        [SerializeField] private ParticleSystemRenderMode renderMode = ParticleSystemRenderMode.Billboard;
        [SerializeField] private ParticleSystemRenderSpace renderAlignment = ParticleSystemRenderSpace.View;
        [SerializeField] private ParticleSystemSortMode sortMode = ParticleSystemSortMode.None;
        [SerializeField] private bool castShadows;
        [SerializeField] private bool receiveShadows;

        [Header("Web Budget Notes")]
        [SerializeField, TextArea(2, 4)] private string notes = "Keep total live transparent particles around 1000 for WebGL. Avoid collision, lights, soft particles and very large overdraw.";

        public Material Material => material;
        public string Notes => notes;

        private void OnValidate()
        {
            maxParticles = Mathf.Max(1, maxParticles);
            duration = Mathf.Max(0.05f, duration);
            lifetime = ClampMinMax(lifetime, 0.01f);
            speed = ClampMinMax(speed, 0f);
            size = ClampMinMax(size, 0.001f);
            noiseFrequency = Mathf.Max(0.01f, noiseFrequency);
        }

        public void ApplyTo(ParticleSystem particleSystem, float intensity, Material fallbackMaterial)
        {
            if (particleSystem == null)
            {
                return;
            }

            var safeIntensity = Mathf.Max(0f, intensity);
            ConfigureMain(particleSystem, safeIntensity);
            ConfigureEmission(particleSystem, safeIntensity);
            ConfigureShape(particleSystem);
            ConfigureColor(particleSystem);
            ConfigureNoise(particleSystem, safeIntensity);
            ConfigureRenderer(particleSystem, fallbackMaterial);
        }

        public void ConfigureAsSmoke()
        {
            maxParticles = 350;
            duration = 3f;
            looping = true;
            emissionRate = 35f;
            lifetime = new Vector2(1.4f, 2.6f);
            speed = new Vector2(0.2f, 0.8f);
            size = new Vector2(0.25f, 0.9f);
            gravityModifier = -0.05f;
            shape = ShapePreset.Cone;
            shapeRadius = 0.2f;
            coneAngle = 35f;
            noiseEnabled = true;
            noiseStrength = 0.45f;
            noiseFrequency = 0.35f;
            colorOverLifetime = CreateDefaultGradient(new Color(0.45f, 0.43f, 0.4f, 0.65f));
        }

        public void ConfigureAsSandStorm()
        {
            maxParticles = 600;
            duration = 2f;
            looping = true;
            emissionRate = 120f;
            lifetime = new Vector2(0.8f, 1.8f);
            speed = new Vector2(1.5f, 4f);
            size = new Vector2(0.05f, 0.22f);
            gravityModifier = 0f;
            shape = ShapePreset.Cone;
            shapeRadius = 1.2f;
            coneAngle = 12f;
            noiseEnabled = true;
            noiseStrength = 0.9f;
            noiseFrequency = 0.8f;
            colorOverLifetime = CreateDefaultGradient(new Color(0.95f, 0.72f, 0.35f, 0.55f));
        }

        public void ConfigureAsCometTrail()
        {
            maxParticles = 450;
            duration = 2f;
            looping = true;
            emissionRate = 90f;
            lifetime = new Vector2(0.25f, 0.9f);
            speed = new Vector2(0.1f, 1.2f);
            size = new Vector2(0.08f, 0.45f);
            gravityModifier = 0f;
            shape = ShapePreset.Cone;
            shapeRadius = 0.08f;
            coneAngle = 18f;
            noiseEnabled = true;
            noiseStrength = 0.25f;
            noiseFrequency = 0.65f;
            colorOverLifetime = CreateDefaultGradient(new Color(1f, 0.42f, 0.08f, 0.8f));
        }

        public void ConfigureAsTerraformGlow()
        {
            maxParticles = 300;
            duration = 1.4f;
            looping = true;
            emissionRate = 70f;
            lifetime = new Vector2(0.45f, 1.2f);
            speed = new Vector2(0.25f, 1.8f);
            size = new Vector2(0.04f, 0.18f);
            gravityModifier = -0.03f;
            shape = ShapePreset.Hemisphere;
            shapeRadius = 0.8f;
            coneAngle = 25f;
            noiseEnabled = true;
            noiseStrength = 0.3f;
            noiseFrequency = 0.55f;
            colorOverLifetime = CreateDefaultGradient(new Color(0.25f, 1f, 0.45f, 0.75f));
        }

        private void ConfigureMain(ParticleSystem particleSystem, float intensity)
        {
            var main = particleSystem.main;
            main.duration = duration;
            main.loop = looping;
            main.prewarm = looping && prewarm;
            main.maxParticles = Mathf.Max(1, Mathf.RoundToInt(maxParticles * Mathf.Max(0.1f, intensity)));
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x * intensity, speed.y * intensity);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f * randomRotation);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white);
            main.gravityModifier = gravityModifier;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.stopAction = ParticleSystemStopAction.Callback;
        }

        private void ConfigureEmission(ParticleSystem particleSystem, float intensity)
        {
            var emission = particleSystem.emission;
            emission.enabled = emissionRate > 0f || burstCount > 0;
            emission.rateOverTime = emissionRate * intensity;
            emission.rateOverDistance = 0f;

            if (burstCount > 0)
            {
                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(burstCount * intensity))
                });
            }
            else
            {
                emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());
            }
        }

        private void ConfigureShape(ParticleSystem particleSystem)
        {
            var shapeModule = particleSystem.shape;
            shapeModule.enabled = true;
            shapeModule.radius = shapeRadius;
            shapeModule.angle = coneAngle;

            shapeModule.shapeType = shape switch
            {
                ShapePreset.Sphere => ParticleSystemShapeType.Sphere,
                ShapePreset.Hemisphere => ParticleSystemShapeType.Hemisphere,
                ShapePreset.Circle => ParticleSystemShapeType.Circle,
                _ => ParticleSystemShapeType.Cone
            };
        }

        private void ConfigureColor(ParticleSystem particleSystem)
        {
            var colorModule = particleSystem.colorOverLifetime;
            colorModule.enabled = true;
            colorModule.color = colorOverLifetime;
        }

        private void ConfigureNoise(ParticleSystem particleSystem, float intensity)
        {
            var noise = particleSystem.noise;
            noise.enabled = noiseEnabled && noiseStrength > 0f;
            noise.strength = noiseStrength * Mathf.Max(0.1f, intensity);
            noise.frequency = noiseFrequency;
            noise.scrollSpeed = noiseScrollSpeed;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }

        private void ConfigureRenderer(ParticleSystem particleSystem, Material fallbackMaterial)
        {
            var particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer == null)
            {
                return;
            }

            particleRenderer.renderMode = renderMode;
            particleRenderer.alignment = renderAlignment;
            particleRenderer.sortMode = sortMode;
            particleRenderer.sharedMaterial = material != null ? material : fallbackMaterial;
            particleRenderer.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = receiveShadows;
        }

        private static Vector2 ClampMinMax(Vector2 value, float minValue)
        {
            value.x = Mathf.Max(minValue, value.x);
            value.y = Mathf.Max(value.x, value.y);
            return value;
        }

        private static Gradient CreateDefaultGradient(Color color)
        {
            var transparent = color;
            transparent.a = 0f;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(color.a, 0.15f),
                    new GradientAlphaKey(color.a * 0.7f, 0.65f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }
    }
}

using LittlePlanet.Particles;
using UnityEditor;
using UnityEngine;

namespace LittlePlanet.EditorTools
{
    public static class UniversalParticlePresetFactory
    {
        private const string RootFolder = "Assets/Particles";
        private const string PresetsFolder = RootFolder + "/Presets";
        private const string PrefabsFolder = RootFolder + "/Prefabs";
        private const string MaterialsFolder = RootFolder + "/Materials";
        private const string ParticleMaterialPath = MaterialsFolder + "/UniversalParticleUnlit.mat";
        private const string ParticleShaderName = "LittlePlanet/UniversalParticleUnlit";

        [MenuItem("Tools/Little Planet/Particles/Create Universal Particle Presets")]
        public static void CreatePresets()
        {
            EnsureFolder("Assets", "Particles");
            EnsureFolder(RootFolder, "Presets");

            var material = EnsureParticleMaterial();
            CreatePreset("Smoke.asset", material, preset => preset.ConfigureAsSmoke());
            CreatePreset("SandStorm.asset", material, preset => preset.ConfigureAsSandStorm());
            CreatePreset("CometTrail.asset", material, preset => preset.ConfigureAsCometTrail());
            CreatePreset("TerraformGlow.asset", material, preset => preset.ConfigureAsTerraformGlow());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Particle Presets", "Universal particle presets created in Assets/Particles/Presets.", "OK");
        }

        [MenuItem("Tools/Little Planet/Particles/Create Universal Particle Prefab")]
        public static void CreatePrefab()
        {
            EnsureFolder("Assets", "Particles");
            EnsureFolder(RootFolder, "Prefabs");
            var material = EnsureParticleMaterial();

            var prefabPath = PrefabsFolder + "/UniversalParticleEffect.prefab";
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab != null)
            {
                AssignFallbackMaterial(existingPrefab, material);
                Selection.activeObject = existingPrefab;
                return;
            }

            var instance = new GameObject("UniversalParticleEffect");
            instance.AddComponent<ParticleSystem>();
            var effect = instance.AddComponent<UniversalParticleEffect>();
            AssignFallbackMaterial(effect, material);
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static void CreatePreset(string assetName, Material material, System.Action<UniversalParticlePreset> configure)
        {
            var path = PresetsFolder + "/" + assetName;
            var preset = AssetDatabase.LoadAssetAtPath<UniversalParticlePreset>(path);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<UniversalParticlePreset>();
                AssetDatabase.CreateAsset(preset, path);
            }

            configure?.Invoke(preset);
            AssignPresetMaterial(preset, material);
            EditorUtility.SetDirty(preset);
        }

        private static Material EnsureParticleMaterial()
        {
            EnsureFolder("Assets", "Particles");
            EnsureFolder(RootFolder, "Materials");

            var material = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
            if (material != null)
            {
                return material;
            }

            var shader = Shader.Find(ParticleShaderName)
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogError($"Cannot create particle material: no compatible shader found. Expected {ParticleShaderName}.");
                return null;
            }

            material = new Material(shader)
            {
                name = "UniversalParticleUnlit"
            };
            AssetDatabase.CreateAsset(material, ParticleMaterialPath);
            return material;
        }

        private static void AssignPresetMaterial(UniversalParticlePreset preset, Material material)
        {
            if (preset == null || material == null)
            {
                return;
            }

            var serializedPreset = new SerializedObject(preset);
            serializedPreset.FindProperty("material").objectReferenceValue = material;
            serializedPreset.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignFallbackMaterial(GameObject prefab, Material material)
        {
            if (prefab == null || material == null)
            {
                return;
            }

            var effect = prefab.GetComponent<UniversalParticleEffect>();
            if (effect == null)
            {
                return;
            }

            AssignFallbackMaterial(effect, material);
            EditorUtility.SetDirty(effect);
            EditorUtility.SetDirty(prefab);
        }

        private static void AssignFallbackMaterial(UniversalParticleEffect effect, Material material)
        {
            if (effect == null || material == null)
            {
                return;
            }

            var serializedEffect = new SerializedObject(effect);
            serializedEffect.FindProperty("fallbackMaterial").objectReferenceValue = material;
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}

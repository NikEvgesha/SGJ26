using LittlePlanet.PlanetSystem;
using UnityEngine;

namespace LittlePlanet.UI
{
    [DefaultExecutionOrder(-150)]
    public sealed class WinScreenController : MonoBehaviour
    {
        [SerializeField] private Planet planet;
        [SerializeField] private GameObject winScreen;
        [SerializeField] private string winScreenObjectName = "WinScreen";
        [SerializeField, Range(0f, 100f)] private float requiredTerraformingPercent = 100f;
        [SerializeField, Range(0f, 100f)] private float requiredOceanPercent = 100f;
        [SerializeField, Min(0f)] private float completionTolerance = 0.01f;
        [SerializeField] private bool resetTimeScaleOnStart = true;

        private bool _isWinShown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceExists()
        {
            if (FindFirstObjectByType<WinScreenController>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            var controllerObject = new GameObject(nameof(WinScreenController));
            controllerObject.AddComponent<WinScreenController>();
        }

        private void Awake()
        {
            ResolveReferences();
            HideWinScreen();
            if (resetTimeScaleOnStart)
            {
                Time.timeScale = 1f;
            }
        }

        private void OnEnable()
        {
            ResolveReferences();
            HideWinScreen();
        }

        private void Update()
        {
            if (_isWinShown)
            {
                return;
            }

            ResolveReferences();
            if (planet == null)
            {
                return;
            }

            var terraformingPercent = planet.GetTerraformingPercent();
            var oceanPercent = planet.GetOceanIndexPercent();
            if (terraformingPercent + completionTolerance < requiredTerraformingPercent
                || oceanPercent + completionTolerance < requiredOceanPercent)
            {
                return;
            }

            ShowWinScreen();
        }

        private void ResolveReferences()
        {
            planet ??= FindFirstObjectByType<Planet>(FindObjectsInactive.Include);
            winScreen ??= FindSceneObjectByName(winScreenObjectName);
        }

        private void HideWinScreen()
        {
            if (winScreen != null)
            {
                winScreen.SetActive(false);
            }
        }

        private void ShowWinScreen()
        {
            _isWinShown = true;
            ResolveReferences();
            if (winScreen != null)
            {
                winScreen.SetActive(true);
            }

            Time.timeScale = 0f;
        }

        private static GameObject FindSceneObjectByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            var objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (var i = 0; i < objects.Length; i++)
            {
                var target = objects[i];
                if (target == null || !target.scene.IsValid())
                {
                    continue;
                }

                if (string.Equals(target.name, objectName, System.StringComparison.Ordinal))
                {
                    return target;
                }
            }

            return null;
        }
    }
}

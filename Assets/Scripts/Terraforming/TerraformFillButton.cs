using UnityEngine;
using UnityEngine.UI;

namespace SGJ26.Terraforming
{
    [RequireComponent(typeof(Button))]
    public sealed class TerraformFillButton : MonoBehaviour
    {
        [SerializeField] private PlanetTerraformController terraformController;
        [SerializeField] private PlanetSequenceManager sequenceManager;
        [SerializeField] private Button button;

        private void Reset()
        {
            button = GetComponent<Button>();
            terraformController = FindFirstObjectByType<PlanetTerraformController>();
            sequenceManager = FindFirstObjectByType<PlanetSequenceManager>();
        }

        private void Awake()
        {
            button ??= GetComponent<Button>();

            if (terraformController == null)
                terraformController = FindFirstObjectByType<PlanetTerraformController>();

            if (sequenceManager == null)
                sequenceManager = FindFirstObjectByType<PlanetSequenceManager>();

            if (button != null)
                button.onClick.AddListener(Fill);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(Fill);
        }

        public void Fill()
        {
            if (sequenceManager == null)
                sequenceManager = FindFirstObjectByType<PlanetSequenceManager>();

            if (sequenceManager != null)
                terraformController = sequenceManager.CurrentPlanet;

            if (terraformController == null)
                terraformController = FindFirstObjectByType<PlanetTerraformController>();

            if (terraformController != null)
                terraformController.FillTerraformingComplete();
        }
    }
}

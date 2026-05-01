using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SGJ26.Terraforming
{
    public sealed class TerraformProgressText : MonoBehaviour
    {
        [SerializeField] private PlanetTerraformController terraformController;
        [SerializeField] private PlanetSequenceManager sequenceManager;
        [SerializeField] private TMP_Text tmpText;
        [SerializeField] private Text uiText;
        [SerializeField] private string format = "Озеленение: {0:0}%";

        private void Reset()
        {
            terraformController = FindFirstObjectByType<PlanetTerraformController>();
            sequenceManager = FindFirstObjectByType<PlanetSequenceManager>();
            tmpText = GetComponent<TMP_Text>();
            uiText = GetComponent<Text>();
        }

        private void Update()
        {
            if (sequenceManager == null)
                sequenceManager = FindFirstObjectByType<PlanetSequenceManager>();

            if (sequenceManager != null)
                terraformController = sequenceManager.CurrentPlanet;

            if (terraformController == null)
                terraformController = FindFirstObjectByType<PlanetTerraformController>();

            if (terraformController == null)
                return;

            string value = string.Format(format, terraformController.VisibleTerraformProgress * 100f);

            if (tmpText != null)
                tmpText.text = value;

            if (uiText != null)
                uiText.text = value;
        }
    }
}

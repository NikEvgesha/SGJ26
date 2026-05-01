using UnityEngine;

namespace SGJ26.Terraforming
{
    [DisallowMultipleComponent]
    public sealed class PlanetVertexColorMaterial : MonoBehaviour
    {
        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private bool logSetup;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private void Start()
        {
            if (applyOnStart)
                Apply();
        }

        [ContextMenu("Apply Vertex Color Material")]
        public void Apply()
        {
            Renderer targetRenderer = GetComponent<Renderer>();
            if (targetRenderer == null)
            {
                if (logSetup)
                    Debug.LogWarning("[PlanetVertexColorMaterial] No Renderer found.", this);

                return;
            }

            Shader shader = Shader.Find("SGJ26/Terraforming/Vertex Color URP");
            if (shader == null)
            {
                if (logSetup)
                    Debug.LogWarning("[PlanetVertexColorMaterial] Shader not found: SGJ26/Terraforming/Vertex Color URP", this);

                return;
            }

            Material material = new Material(shader)
            {
                name = "Planet Vertex Color Runtime"
            };
            material.SetColor(BaseColor, Color.white);
            targetRenderer.sharedMaterial = material;

        }
    }
}

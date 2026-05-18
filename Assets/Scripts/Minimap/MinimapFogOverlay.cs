using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.Minimap
{
    public class MinimapFogOverlay : MonoBehaviour
    {
        private static readonly int FogColorId = Shader.PropertyToID("_FogColor");

        [SerializeField] private RawImage rawImage;
        [SerializeField] private RenderTexture exploredFogTexture;
        [SerializeField] private Material unexploredFogMaterial;
        [SerializeField] [Range(0f, 1f)] private float unexploredFogAlpha = 0.75f;
        [SerializeField] private Color unexploredFogColor = new(0f, 0f, 0f, 1f);

        private void Awake()
        {
            if (rawImage == null)
            {
                rawImage = GetComponent<RawImage>();
            }

            ApplyTexture();
        }

        public void SetExploredTexture(RenderTexture texture)
        {
            exploredFogTexture = texture;
            ApplyTexture();
        }

        private void ApplyTexture()
        {
            if (rawImage == null)
            {
                return;
            }

            rawImage.texture = exploredFogTexture;
            rawImage.color = Color.white;

            Material material = unexploredFogMaterial;
            if (material == null)
            {
                Shader shader = Shader.Find("UI/MinimapUnexploredFog");
                if (shader != null)
                {
                    material = new Material(shader);
                }
            }

            if (material != null)
            {
                rawImage.material = material;
                Color fog = unexploredFogColor;
                fog.a = unexploredFogAlpha;
                material.SetColor(FogColorId, fog);
            }
        }
    }
}

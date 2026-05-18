using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Minimap
{
    [CreateAssetMenu(fileName = "Minimap Icon Style", menuName = "RTS/Minimap/Icon Style")]
    public class MinimapIconStyleSO : ScriptableObject
    {
        [SerializeField] private Sprite iconSprite;
        [SerializeField] private Color playerColor = new(0.2f, 0.85f, 1f, 1f);
        [SerializeField] private Color enemyColor = new(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color neutralColor = new(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Vector2 iconSize = new(10f, 10f);

        public Sprite IconSprite => iconSprite;
        public Vector2 IconSize => iconSize;

        public Color GetColor(Owner owner)
        {
            if (owner == Owner.Player1)
            {
                return playerColor;
            }

            if (owner == Owner.Unowned)
            {
                return neutralColor;
            }

            return enemyColor;
        }
    }
}

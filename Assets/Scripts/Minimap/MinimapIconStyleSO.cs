using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Minimap
{
    [CreateAssetMenu(fileName = "Minimap Icon Style", menuName = "RTS/Minimap/Icon Style")]
    public class MinimapIconStyleSO : ScriptableObject
    {
        [SerializeField] private Sprite dotSprite;
        [SerializeField] private Sprite fallbackUnitIconSprite;
        [SerializeField] private Sprite buildingIconSprite;
        [SerializeField] private Color playerColor = Color.white;
        [SerializeField] private Color enemyColor = new(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color neutralColor = new(0.35f, 0.95f, 0.3f, 1f);
        [SerializeField] private Color allyBuildingColor = Color.white;
        [SerializeField] private Color enemyBuildingColor = new(1f, 0.35f, 0.3f, 1f);
        [SerializeField] private Vector2 unitIconSize = new(12f, 12f);
        [SerializeField] private Vector2 buildingIconSize = new(16f, 16f);
        [SerializeField] private Vector2 supplyIconSize = new(10f, 10f);
        [SerializeField] private Color supplyColor = new(1f, 1f, 1f, 1f);

        private static Sprite whiteFallbackSprite;

        /// <summary>
        /// Mục tiêu: Tạo dữ liệu hiển thị icon minimap từ unit/building (sprite UnitSO + màu theo Owner).
        /// Cách hoạt động: Ưu tiên UnitSO.Icon; fallback dot; đổi size/màu nếu là building.
        /// </summary>
        public MinimapMarkerPresentation BuildFor(AbstractCommandable commandable)
        {
            bool isBuilding = commandable is BaseBuilding;
            Sprite sprite = commandable.UnitSO != null ? commandable.UnitSO.Icon : null;
            if (sprite == null)
            {
                sprite = isBuilding ? buildingIconSprite : fallbackUnitIconSprite;
            }

            if (sprite == null)
            {
                sprite = dotSprite;
            }

            Color color = ResolveOwnerColor(commandable.Owner, isBuilding);
            Vector2 size = isBuilding ? buildingIconSize : unitIconSize;
            bool isUnitIcon = sprite != dotSprite && sprite != fallbackUnitIconSprite;
            return new MinimapMarkerPresentation(sprite, color, size, isUnitIcon);
        }

        /// <summary>
        /// Mục tiêu: Tạo dữ liệu icon minimap cho supply (wood/stone/food…).
        /// Cách hoạt động: Lấy SupplySO.Icon; nếu null dùng sprite trắng 1×1 tạo sẵn.
        /// </summary>
        public MinimapMarkerPresentation BuildForSupply(GatherableSupply supply)
        {
            Sprite sprite = supply != null && supply.Supply != null ? supply.Supply.Icon : null;
            if (sprite == null)
            {
                sprite = GetWhiteFallbackSprite();
            }

            bool isSupplyIcon = supply != null && supply.Supply != null && supply.Supply.Icon != null;
            return new MinimapMarkerPresentation(sprite, supplyColor, supplyIconSize, isSupplyIcon);
        }

        private static Sprite GetWhiteFallbackSprite()
        {
            if (whiteFallbackSprite != null)
            {
                return whiteFallbackSprite;
            }

            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, false);

            whiteFallbackSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            return whiteFallbackSprite;
        }

        private Color ResolveOwnerColor(Owner owner, bool isBuilding)
        {
            if (isBuilding)
            {
                return owner == Owner.Player1 ? allyBuildingColor : enemyBuildingColor;
            }

            return owner switch
            {
                Owner.Player1 => playerColor,
                Owner.Unowned => neutralColor,
                _ => enemyColor,
            };
        }
    }
}

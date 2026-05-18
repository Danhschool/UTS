using UnityEngine;

namespace GameDevTV.RTS.Minimap
{
    public readonly struct MinimapMarkerPresentation
    {
        public Sprite Sprite { get; }
        public Color Color { get; }
        public Vector2 Size { get; }
        public bool IsUnitIcon { get; }

        public MinimapMarkerPresentation(Sprite sprite, Color color, Vector2 size, bool isUnitIcon)
        {
            Sprite = sprite;
            Color = color;
            Size = size;
            IsUnitIcon = isUnitIcon;
        }
    }
}

using System;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.UI
{
    [CreateAssetMenu(menuName = "RTS/UI/Owner Health Bar Style", fileName = "OwnerHealthBarStyles")]
    public class OwnerHealthBarStyleSO : ScriptableObject
    {
        [Serializable]
        public struct Style
        {
            public Sprite borderSprite;
            public Sprite fillSprite;
        }

        [SerializeField] private Style player1Style;
        [SerializeField] private Style ai1Style;
        [SerializeField] private Style defaultStyle;

        public bool TryGetStyle(Owner owner, out Style style)
        {
            switch (owner)
            {
                case Owner.Player1:
                    style = player1Style;
                    return style.borderSprite != null || style.fillSprite != null;
                case Owner.AI1:
                    style = ai1Style;
                    return style.borderSprite != null || style.fillSprite != null;
                default:
                    style = defaultStyle;
                    return style.borderSprite != null || style.fillSprite != null;
            }
        }
    }
}

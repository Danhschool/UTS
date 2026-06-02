using System;
using UnityEngine;

namespace GameDevTV.RTS.UI.Pregame
{
    /// <summary>
    /// Dữ liệu một map trên màn setup (tên hiển thị, ảnh minh họa, scene gameplay).
    /// </summary>
    [Serializable]
    public struct PregameMapEntry
    {
        public string displayName;
        public Sprite previewSprite;
        public string gameplaySceneName;
    }
}

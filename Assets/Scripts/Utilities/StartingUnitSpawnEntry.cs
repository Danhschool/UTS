using System;
using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    /// <summary>
    /// SRP: Một dòng cấu hình spawn unit khởi đầu (prefab + số lượng).
    /// </summary>
    [Serializable]
    public struct StartingUnitSpawnEntry
    {
        public GameObject unitPrefab;

        [Min(0)]
        public int count;
    }
}

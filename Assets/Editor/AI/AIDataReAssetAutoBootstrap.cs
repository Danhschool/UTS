#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace GameDevTV.RTS.AI.Editor
{
    /// <summary>
    /// Tạo difficulty assets lần đầu mở project nếu thiếu (không ghi đè asset đã có).
    /// </summary>
    [InitializeOnLoad]
    static class AIDataReAssetAutoBootstrap
    {
        const string MediumPath = "Assets/Data_Re/AI/Difficulty/AIDifficulty_Medium.asset";

        static AIDataReAssetAutoBootstrap()
        {
            EditorApplication.delayCall += TryCreateIfMissing;
        }

        static void TryCreateIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (File.Exists(MediumPath))
            {
                return;
            }

            AIDataReAssetCreator.CreateAll();
        }
    }
}
#endif

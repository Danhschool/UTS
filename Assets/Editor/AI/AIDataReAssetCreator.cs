#if UNITY_EDITOR
using System.IO;
using GameDevTV.RTS.AI;
using UnityEditor;
using UnityEngine;

namespace GameDevTV.RTS.AI.Editor
{
    /// <summary>
    /// SRP: Tạo 3 AIDifficultySO + AIGameSessionConfigSO trong Data_Re (menu Editor).
    /// </summary>
    public static class AIDataReAssetCreator
    {
        const string Root = "Assets/Data_Re/AI";
        const string DifficultyDir = Root + "/Difficulty";
        const string EasyPath = DifficultyDir + "/AIDifficulty_Easy.asset";
        const string MediumPath = DifficultyDir + "/AIDifficulty_Medium.asset";
        const string HardPath = DifficultyDir + "/AIDifficulty_Hard.asset";
        const string SessionPath = Root + "/AIGameSessionConfig.asset";

        [MenuItem("RTS/AI/Create Data_Re AI Assets")]
        public static void CreateAll()
        {
            EnsureFolder(Root);
            EnsureFolder(DifficultyDir);

            AIDifficultySO easy = LoadOrCreateDifficulty(EasyPath, ApplyEasy);
            AIDifficultySO medium = LoadOrCreateDifficulty(MediumPath, ApplyMedium);
            AIDifficultySO hard = LoadOrCreateDifficulty(HardPath, ApplyHard);

            AIGameSessionConfigSO session = AssetDatabase.LoadAssetAtPath<AIGameSessionConfigSO>(SessionPath);
            if (session == null)
            {
                session = ScriptableObject.CreateInstance<AIGameSessionConfigSO>();
                AssetDatabase.CreateAsset(session, SessionPath);
            }

            SerializedObject so = new(session);
            so.FindProperty("easy").objectReferenceValue = easy;
            so.FindProperty("medium").objectReferenceValue = medium;
            so.FindProperty("hard").objectReferenceValue = hard;
            so.FindProperty("defaultDifficulty").enumValueIndex = (int)AIDifficultyLevel.Medium;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(session);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[RTS AI] Created/updated difficulty assets under {DifficultyDir} and {SessionPath}. " +
                "Assign AIGameSessionConfig on GameSetup or AIController.SetDifficulty at runtime.");
        }

        static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
                string name = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolder(parent);
                }

                AssetDatabase.CreateFolder(parent, name);
            }
        }

        static AIDifficultySO LoadOrCreateDifficulty(string path, System.Action<AIDifficultySO> applyPreset)
        {
            AIDifficultySO asset = AssetDatabase.LoadAssetAtPath<AIDifficultySO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<AIDifficultySO>();
                applyPreset(asset);
                AssetDatabase.CreateAsset(asset, path);
            }
            else
            {
                applyPreset(asset);
                EditorUtility.SetDirty(asset);
            }

            return asset;
        }

        static void ApplyEasy(AIDifficultySO so) => so.ApplyEasyPreset();
        static void ApplyMedium(AIDifficultySO so) => so.ApplyMediumPreset();
        static void ApplyHard(AIDifficultySO so) => so.ApplyHardPreset();
    }
}
#endif

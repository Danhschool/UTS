#if UNITY_EDITOR
using GameDevTV.RTS.Hotkeys;
using UnityEditor;
using UnityEngine;

namespace GameDevTV.RTS.Editor.Hotkeys
{
    [CustomEditor(typeof(UnitTypeHotkeySetup))]
    public sealed class UnitTypeHotkeySetupEditor : UnityEditor.Editor
    {
        const string WorkerPath = "Assets/Prefab/Unit/Worker 1.prefab";
        const string WarriorPath = "Assets/Prefab/Unit/Worrior.prefab";
        const string ArcherPath = "Assets/Prefab/Unit/Archer.prefab";
        const string RockWarriorPath = "Assets/Prefab/Unit/RockWarrior 1.prefab";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("Gán prefab mặc định (Worker / Warrior / Archer / RockWarrior)"))
            {
                WireDefaultPrefabs((UnitTypeHotkeySetup)target);
            }
        }

        [MenuItem("ProjectRTS/Hotkeys/Wire default unit type prefabs on selected UnitTypeHotkeySetup")]
        static void WireSelected()
        {
            foreach (Object obj in Selection.objects)
            {
                if (obj is UnitTypeHotkeySetup setup)
                {
                    WireDefaultPrefabs(setup);
                }
            }
        }

        static void WireDefaultPrefabs(UnitTypeHotkeySetup setup)
        {
            SerializedObject so = new SerializedObject(setup);
            so.FindProperty("workerPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(WorkerPath);
            so.FindProperty("warriorPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(WarriorPath);
            so.FindProperty("archerPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(ArcherPath);
            so.FindProperty("rockWarriorPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(RockWarriorPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(setup);
            Debug.Log($"[{nameof(UnitTypeHotkeySetupEditor)}] Đã gán 4 prefab unit type cho {setup.name}.");
        }
    }
}
#endif

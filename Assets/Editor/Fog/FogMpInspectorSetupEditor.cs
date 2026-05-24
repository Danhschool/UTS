#if UNITY_EDITOR
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Editor.Fog
{
    /// <summary>
    /// Mục tiêu: M2 Inspector — layer P2, duplicate fog prefab, wire PlayerViewBinder trên Game 1.
    /// Cách hoạt động: Menu Editor; không tạo .meta thủ công (Unity sinh khi import).
    /// </summary>
    public static class FogMpInspectorSetupEditor
    {
        const string FogP1PrefabPath = "Assets/Prefab/Fog of War 1.prefab";
        const string FogP2PrefabPath = "Assets/Prefab/Fog of War Player2.prefab";
        const string Game1ScenePath = "Assets/Scenes/Game 1.unity";
        const string ExploredRtP2Path = "Assets/Textures/Explored Fog of War Render Texture P2.renderTexture";
        const string VisionRtP2Path = "Assets/Textures/Fog of War Render Texture P2.renderTexture";
        const string FogMatP2Path = "Assets/Materials/Fog of War P2.mat";

        [MenuItem("ProjectRTS/Fog MP/Setup M2 (layer + P2 prefab + Game 1 wire)")]
        public static void RunFullSetup()
        {
            EnsurePlayer2VisionLayer();
            GameObject p2Prefab = EnsureFogPlayer2Prefab();
            WireGame1Scene(p2Prefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[FogMpInspectorSetup] M2 xong. Mở Game 1 và kiểm tra Fog P1/P2 + PlayerViewBinder.");
        }

        [MenuItem("ProjectRTS/Fog MP/Ensure Fog Vision Player2 layer")]
        public static void EnsurePlayer2VisionLayer()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            bool ok = SetLayerName(layers, OwnerFogVisionLayers.DefaultPlayer2VisionLayer, "Fog Vision Player2");
            if (ok)
            {
                tagManager.ApplyModifiedProperties();
                Debug.Log("[FogMpInspectorSetup] Layer Fog Vision Player2 (15) đã sẵn sàng.");
            }
        }

        [MenuItem("ProjectRTS/Fog MP/Duplicate Fog of War Player2 prefab")]
        public static GameObject EnsureFogPlayer2PrefabMenu() => EnsureFogPlayer2Prefab();

        static GameObject EnsureFogPlayer2Prefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(FogP2PrefabPath);
            if (existing != null)
            {
                ConfigureFogPrefabForPlayer2(existing);
                return existing;
            }

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(FogP1PrefabPath);
            if (source == null)
            {
                Debug.LogError($"[FogMpInspectorSetup] Không tìm thấy {FogP1PrefabPath}");
                return null;
            }

            GameObject duplicate = Object.Instantiate(source);
            duplicate.name = "Fog of War Player2";
            ConfigureFogPrefabForPlayer2(duplicate);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(duplicate, FogP2PrefabPath);
            Object.DestroyImmediate(duplicate);
            return prefab;
        }

        static void ConfigureFogPrefabForPlayer2(GameObject root)
        {
            int visionLayer = OwnerFogVisionLayers.GetLayer(Owner.Player2);
            int visionMask = 1 << visionLayer;

            RenderTexture exploredRt = AssetDatabase.LoadAssetAtPath<RenderTexture>(ExploredRtP2Path);
            RenderTexture visionRt = AssetDatabase.LoadAssetAtPath<RenderTexture>(VisionRtP2Path);
            Material fogMat = AssetDatabase.LoadAssetAtPath<Material>(FogMatP2Path);

            Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null)
                {
                    continue;
                }

                camera.cullingMask = visionMask;
                if (camera.gameObject.name.Contains("Explored") && exploredRt != null)
                {
                    camera.targetTexture = exploredRt;
                }
                else if ((camera.gameObject.name.Contains("Visibility") || camera.gameObject.name.Contains("Vision"))
                         && visionRt != null)
                {
                    camera.targetTexture = visionRt;
                }

                camera.enabled = false;
            }

            MeshRenderer plane = root.GetComponentInChildren<MeshRenderer>(true);
            if (plane != null && fogMat != null)
            {
                plane.sharedMaterial = fogMat;
            }

            FactionFogPresentation presentation = root.GetComponent<FactionFogPresentation>();
            if (presentation == null)
            {
                presentation = root.AddComponent<FactionFogPresentation>();
            }

            SerializedObject so = new SerializedObject(presentation);
            so.FindProperty("presentationOwner").intValue = (int)Owner.Player2;
            so.FindProperty("presentationRoot").objectReferenceValue = root;
            so.ApplyModifiedPropertiesWithoutUndo();

            FactionFogSystemReference reference = root.GetComponent<FactionFogSystemReference>();
            if (reference == null)
            {
                reference = root.AddComponent<FactionFogSystemReference>();
            }

            reference.ConfigureFaction(Owner.Player2);
            root.SetActive(false);
        }

        [MenuItem("ProjectRTS/Fog MP/Wire Game 1 fog branches")]
        public static void WireGame1Menu()
        {
            WireGame1Scene(AssetDatabase.LoadAssetAtPath<GameObject>(FogP2PrefabPath));
        }

        static void WireGame1Scene(GameObject fogP2Prefab)
        {
            Scene scene = EditorSceneManager.OpenScene(Game1ScenePath, OpenSceneMode.Single);
            PlayerViewBinder binder = Object.FindFirstObjectByType<PlayerViewBinder>(FindObjectsInactive.Include);
            if (binder == null)
            {
                GameObject binderGo = new GameObject("PlayerViewBinder");
                binder = binderGo.AddComponent<PlayerViewBinder>();
            }

            FactionFogPresentation p1 = FindPresentation(Owner.Player1);
            FactionFogPresentation p2 = FindPresentation(Owner.Player2);

            if (p2 == null && fogP2Prefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fogP2Prefab, scene);
                instance.name = "Fog of War P2";
                p2 = instance.GetComponent<FactionFogPresentation>();
            }

            if (p1 == null)
            {
                Debug.LogWarning("[FogMpInspectorSetup] Không tìm thấy FactionFogPresentation P1 trong scene.");
            }

            SerializedObject binderSo = new SerializedObject(binder);
            binderSo.FindProperty("presentationPlayer1").objectReferenceValue = p1;
            binderSo.FindProperty("presentationPlayer2").objectReferenceValue = p2;
            binderSo.FindProperty("visibilityUpdater").objectReferenceValue =
                Object.FindFirstObjectByType<FactionVisibilityUpdater>(FindObjectsInactive.Include);

            FactionHudBinder hud = Object.FindFirstObjectByType<FactionHudBinder>(FindObjectsInactive.Include);
            if (hud == null)
            {
                hud = binder.gameObject.AddComponent<FactionHudBinder>();
            }

            binderSo.FindProperty("hudBinder").objectReferenceValue = hud;
            binderSo.ApplyModifiedPropertiesWithoutUndo();

            if (Object.FindFirstObjectByType<LocalHumanOwnerBootstrap>(FindObjectsInactive.Include) == null)
            {
                new GameObject("LocalHumanOwnerBootstrap").AddComponent<LocalHumanOwnerBootstrap>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static FactionFogPresentation FindPresentation(Owner owner)
        {
            FactionFogPresentation[] all = Object.FindObjectsByType<FactionFogPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].PresentationOwner == owner)
                {
                    return all[i];
                }
            }

            return null;
        }

        static bool SetLayerName(SerializedProperty layers, int index, string layerName)
        {
            if (index < 0 || index >= layers.arraySize)
            {
                return false;
            }

            SerializedProperty element = layers.GetArrayElementAtIndex(index);
            if (element.stringValue == layerName)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(element.stringValue) && element.stringValue != layerName)
            {
                Debug.LogWarning($"[FogMpInspectorSetup] Layer {index} đang là '{element.stringValue}', không ghi đè.");
                return false;
            }

            element.stringValue = layerName;
            return true;
        }
    }
}
#endif

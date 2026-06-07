#if UNITY_EDITOR
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameDevTV.RTS.Editor.FogOfWar
{
    /// <summary>
    /// Section 09 (Simple Fog of War): nhân bản stack fog P1 → P2 (RT, camera, plane, material) để MP client P2 hoạt động như khóa học.
    /// </summary>
    public static class FogOfWarCourseP2SetupEditor
    {
        const string FogP1PrefabPath = "Assets/Prefab/Fog of War 1.prefab";
        const string FogP2PrefabPath = "Assets/Prefab/Fog of War P2.prefab";
        const string VisionRtP1Path = "Assets/Textures/Fog of War Render Texture.renderTexture";
        const string ExploredRtP1Path = "Assets/Textures/Explored Fog of War Render Texture.renderTexture";
        const string VisionRtP2Path = "Assets/Textures/Fog of War Render Texture P2.renderTexture";
        const string ExploredRtP2Path = "Assets/Textures/Explored Fog of War Render Texture P2.renderTexture";
        const string FogMatP2Path = "Assets/Materials/Fog of War P2.mat";
        const string ExploredMatP2Path = "Assets/Materials/Explored Fog of War P2.mat";
        const string FogOverlayRendererPath = "Assets/Settings/Fog of War_Renderer.asset";

        [MenuItem("ProjectRTS/Fog of War/★ Setup P2 Fog (Course Section 9)")]
        public static void SetupP2FogMenu()
        {
            EnsureP2RenderTexturesFromP1();
            EnsureFogOverlayRendererIncludesP2Plane();
            ApplyCourseSection9ToPrefab(FogP2PrefabPath, Owner.Player2);
            AssetDatabase.SaveAssets();
            Debug.Log("[FogOfWar] Đã áp dụng Section 9 cho prefab Fog of War P2.");
        }

        /// <summary>
        /// Mục tiêu: Overlay camera (Fog of War_Renderer) phải vẽ được plane P2 layer 17 — nếu thiếu, client P2 không bôi đen fog.
        /// Cách hoạt động: OR bitmask layer 13 + 14 + 17 vào Opaque/Transparent mask của UniversalRendererData.
        /// </summary>
        public static void EnsureFogOverlayRendererIncludesP2Plane()
        {
            UniversalRendererData renderer =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(FogOverlayRendererPath);
            if (renderer == null)
            {
                Debug.LogWarning($"[FogOfWar] Không tìm thấy {FogOverlayRendererPath}");
                return;
            }

            int requiredMask = (1 << OwnerFogPlaneLayers.DefaultPlayer1PlaneLayer)
                | (1 << OwnerFogVisionLayers.DefaultPlayer1VisionLayer)
                | (1 << OwnerFogPlaneLayers.DefaultPlayer2PlaneLayer);

            if (renderer.opaqueLayerMask == (LayerMask)requiredMask
                && renderer.transparentLayerMask == (LayerMask)requiredMask)
            {
                return;
            }

            renderer.opaqueLayerMask = requiredMask;
            renderer.transparentLayerMask = requiredMask;
            EditorUtility.SetDirty(renderer);
        }

        /// <summary>
        /// Mục tiêu: Chuẩn bị scene MP — gọi từ RtsNetGameSceneSetupEditor sau khi tìm/instantiate fog P2.
        /// </summary>
        public static void ApplyCourseSection9ToPresentation(FactionFogPresentation fogP2)
        {
            if (fogP2 == null)
            {
                return;
            }

            EnsureP2RenderTexturesFromP1();
            WireFactionFogPresentation(fogP2, Owner.Player2);
            EditorUtility.SetDirty(fogP2);
        }

        public static void ApplyCourseSection9ToPrefab(string prefabPath, Owner owner)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"[FogOfWar] Không load được prefab: {prefabPath}");
                return;
            }

            try
            {
                FactionFogPresentation presentation = prefabRoot.GetComponent<FactionFogPresentation>();
                if (presentation == null)
                {
                    Debug.LogWarning($"[FogOfWar] Thiếu FactionFogPresentation trên {prefabPath}");
                    return;
                }

                WireFactionFogPresentation(presentation, owner);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>
        /// Mục tiêu: Client P2 có RT riêng (vision + explored) — copy từ P1 nếu chưa tạo.
        /// Cách hoạt động: AssetDatabase.CopyAsset từ RT P1 sang đường dẫn P2 chuẩn.
        /// </summary>
        public static void EnsureP2RenderTexturesFromP1()
        {
            CopyRenderTextureIfMissing(VisionRtP1Path, VisionRtP2Path);
            CopyRenderTextureIfMissing(ExploredRtP1Path, ExploredRtP2Path);
        }

        static void CopyRenderTextureIfMissing(string sourcePath, string targetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<RenderTexture>(targetPath) != null)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<RenderTexture>(sourcePath) == null)
            {
                Debug.LogWarning($"[FogOfWar] Thiếu RT nguồn: {sourcePath}");
                return;
            }

            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            {
                Debug.LogWarning($"[FogOfWar] Không copy được {sourcePath} → {targetPath}");
            }
        }

        /// <summary>
        /// Mục tiêu: Một nhánh fog P2 đúng chuẩn khóa (camera → RT P2, mask layer Fog Vision Player2, plane layer 17).
        /// Cách hoạt động: Gán FactionFogSystemReference, camera targetTexture, culling mask, material plane P2.
        /// </summary>
        public static void WireFactionFogPresentation(FactionFogPresentation presentation, Owner owner)
        {
            if (presentation == null || !HumanFogVisionUtility.EmitsFogVision(owner))
            {
                return;
            }

            FactionFogSystemReference systemRef = presentation.FogSystemReference
                ?? presentation.GetComponent<FactionFogSystemReference>();
            if (systemRef == null)
            {
                systemRef = presentation.gameObject.AddComponent<FactionFogSystemReference>();
            }

            RenderTexture visionRt = LoadOwnerRenderTexture(owner, isExplored: false);
            RenderTexture exploredRt = LoadOwnerRenderTexture(owner, isExplored: true);
            Material fogMat = LoadOwnerFogMaterial(owner);

            SerializedObject systemSo = new SerializedObject(systemRef);
            systemSo.FindProperty("factionOwner").intValue = (int)owner;
            systemSo.FindProperty("exploredTexture").objectReferenceValue = exploredRt;
            systemSo.FindProperty("visionTexture").objectReferenceValue = visionRt;
            systemSo.ApplyModifiedPropertiesWithoutUndo();

            Camera[] cameras = presentation.GetComponentsInChildren<Camera>(true);
            Camera explored = null;
            Camera vision = null;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cam = cameras[i];
                if (cam == null || cam.targetTexture == null && exploredRt == null && visionRt == null)
                {
                    continue;
                }

                string name = cam.gameObject.name;
                if (explored == null && name.Contains("Explored"))
                {
                    explored = cam;
                }
                else if (vision == null && (name.Contains("Visibility") || name.Contains("Vision")))
                {
                    vision = cam;
                }
            }

            int visionMask = 1 << OwnerFogVisionLayers.GetLayer(owner);
            ConfigureFogCamera(explored, exploredRt, visionMask, keepFrameRendererIndex: 2);
            ConfigureFogCamera(vision, visionRt, visionMask, keepFrameRendererIndex: 1);

            systemSo = new SerializedObject(systemRef);
            systemSo.FindProperty("exploredFogCamera").objectReferenceValue = explored;
            systemSo.FindProperty("visionFogCamera").objectReferenceValue = vision;
            systemSo.ApplyModifiedPropertiesWithoutUndo();

            MeshRenderer planeRenderer = FindFogPlaneRenderer(presentation.transform);
            if (planeRenderer != null)
            {
                OwnerFogPlaneLayers.ApplyToPlane(planeRenderer.gameObject, owner);
                if (fogMat != null)
                {
                    planeRenderer.sharedMaterial = fogMat;
                }
            }

            SerializedObject presentationSo = new SerializedObject(presentation);
            presentationSo.FindProperty("presentationOwner").intValue = (int)owner;
            presentationSo.FindProperty("exploredFogCamera").objectReferenceValue = explored;
            presentationSo.FindProperty("visionFogCamera").objectReferenceValue = vision;
            presentationSo.FindProperty("fogSystemReference").objectReferenceValue = systemRef;
            presentationSo.FindProperty("fogPlaneRenderer").objectReferenceValue = planeRenderer;
            presentationSo.ApplyModifiedPropertiesWithoutUndo();

            presentation.ApplyOwnerCameraMasks();
            systemRef.EnsureReferences();
            presentation.RefreshFogTextures();
        }

        static RenderTexture LoadOwnerRenderTexture(Owner owner, bool isExplored)
        {
            if (owner == Owner.Player2)
            {
                return AssetDatabase.LoadAssetAtPath<RenderTexture>(
                    isExplored ? ExploredRtP2Path : VisionRtP2Path);
            }

            return AssetDatabase.LoadAssetAtPath<RenderTexture>(
                isExplored ? ExploredRtP1Path : VisionRtP1Path);
        }

        static Material LoadOwnerFogMaterial(Owner owner)
        {
            if (owner == Owner.Player2)
            {
                return AssetDatabase.LoadAssetAtPath<Material>(FogMatP2Path);
            }

            return AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Fog of War.mat");
        }

        static void ConfigureFogCamera(Camera camera, RenderTexture target, int cullingMask, int keepFrameRendererIndex)
        {
            if (camera == null)
            {
                return;
            }

            if (target != null)
            {
                camera.targetTexture = target;
            }

            camera.cullingMask = cullingMask;
            camera.orthographic = true;

            UniversalAdditionalCameraData urp = camera.GetComponent<UniversalAdditionalCameraData>();
            if (urp != null)
            {
                urp.renderShadows = false;
                urp.renderPostProcessing = false;
                if (camera.gameObject.name.Contains("Explored"))
                {
                    urp.SetRenderer(keepFrameRendererIndex);
                }
                else
                {
                    urp.SetRenderer(keepFrameRendererIndex);
                }
            }

            EditorUtility.SetDirty(camera);
        }

        static MeshRenderer FindFogPlaneRenderer(Transform root)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].gameObject.name.Contains("Plane"))
                {
                    return renderers[i];
                }
            }

            return null;
        }
    }
}
#endif

#if UNITY_EDITOR
using GameDevTV.RTS.Editor.FogOfWar;
using GameDevTV.RTS.Editor.Gameplay;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDevTV.RTS.Editor.Netplay
{
    /// <summary>
    /// Mục tiêu: Chuẩn bị sẵn RtsNet_Game — spawn UTS, fog P1/P2, presentation, bootstrap MP.
    /// </summary>
    public static class RtsNetGameSceneSetupEditor
    {
        const string GameScenePath = GameplayMapScenePaths.ReferenceStableScenePath;
        const string FogP2PrefabPath = GameplayMapScenePaths.FogP2PrefabPath;

        [MenuItem("ProjectRTS/Netplay/★ Prepare RtsNet_Game Scene (save to scene)")]
        public static void PrepareSceneMenu()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            PrepareOpenScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[RtsNetGameScene] Đã lưu RtsNet_Game — spawn UTS + MP presentation.");
        }

        public static void ExecuteBatch()
        {
            PrepareSceneMenu();
        }

        public static void PrepareOpenScene()
        {
            GameplayMapSceneSetupApplicator.PrepareOpenGameplayMapScene(wirePresentation: true);
        }
    }

    /// <summary>
    /// Wire presentation — gọi từ Prepare scene.
    /// </summary>
    public static class MpPresentationSceneSetupEditor
    {
        const string GameScenePath = GameplayMapScenePaths.ReferenceStableScenePath;
        const string FogP2PrefabPath = GameplayMapScenePaths.FogP2PrefabPath;

        [MenuItem("ProjectRTS/Netplay/Setup MP Presentation (fog + UI + input per player)")]
        public static void SetupMenu()
        {
            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            WireInOpenScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MpPresentation] Đã wire presentation trên RtsNet_Game.");
        }

        public static void WireInOpenScene()
        {
            EnsureMpPresentationPrerequisites();

            MpPlayerPresentationDirector director =
                Object.FindFirstObjectByType<MpPlayerPresentationDirector>(FindObjectsInactive.Include);

            if (director == null)
            {
                GameObject directorGo = new GameObject("MpPlayerPresentation");
                director = directorGo.AddComponent<MpPlayerPresentationDirector>();
            }

            PlayerInput mainInput = FindMainCameraPlayerInput();
            RemoveRigFromMainCamera(mainInput);

            FactionFogPresentation fogP1 = FindPresentation(Owner.Player1);
            FactionFogPresentation fogP2 = FindPresentation(Owner.Player2);

            if (fogP2 == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FogP2PrefabPath);
                if (prefab != null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
                    instance.name = "Fog of War P2";
                    fogP2 = instance.GetComponent<FactionFogPresentation>();
                }
                else
                {
                    Debug.LogWarning($"[MpPresentation] Không tìm thấy {FogP2PrefabPath} — tạo Fog P2 trong Editor.");
                }
            }

            Supplies hudP1 = FindHudSupplies(Owner.Player1);
            Supplies hudP2 = FindHudSupplies(Owner.Player2);

            MpPlayerPresentationRig rigP1 = EnsureRigOnFogRoot(Owner.Player1, fogP1, mainInput, hudP1);
            MpPlayerPresentationRig rigP2 = EnsureRigOnFogRoot(Owner.Player2, fogP2, mainInput, hudP2);

            FactionVisibilityUpdater visP1 = EnsureVisibilityUpdater(fogP1);
            EnsureVisibilityUpdater(fogP2);

            ApplyFogCameraMasksInEditor(fogP1, Owner.Player1);
            if (fogP2 != null)
            {
                FogOfWarCourseP2SetupEditor.ApplyCourseSection9ToPresentation(fogP2);
            }
            else
            {
                ApplyFogCameraMasksInEditor(fogP2, Owner.Player2);
            }

            SetFogAndHudDefaultsInactive(fogP1, fogP2, hudP1, hudP2);

            SerializedObject directorSo = new SerializedObject(director);
            directorSo.FindProperty("player1Rig").objectReferenceValue = rigP1;
            directorSo.FindProperty("player2Rig").objectReferenceValue = rigP2;
            directorSo.FindProperty("visibilityUpdater").objectReferenceValue = visP1;
            directorSo.FindProperty("hudBinder").objectReferenceValue =
                Object.FindFirstObjectByType<FactionHudBinder>(FindObjectsInactive.Include);
            if (mainInput != null)
            {
                directorSo.FindProperty("sharedGameplayCamera").objectReferenceValue = mainInput.GameplayCamera;
            }

            directorSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);

            WireHudBinderSupplies(hudP1);
            WireRuntimeUiBusOwners(hudP1, Owner.Player1);
            WireRuntimeUiBusOwners(hudP2, Owner.Player2);
            EnsureLocalHumanOwnerOnMainCamera(mainInput);
            EnsureMainCameraFogOverlayPrefab();
            DisableLegacyPlayerViewBinder();
            LogPresentationValidation(fogP1, fogP2, hudP1, hudP2, rigP1, rigP2);
        }

        /// <summary>
        /// Mục tiêu: Map offline thường chỉ có 1 HUD + 1 fog — MP cần bản nhân đôi cho P2.
        /// Cách hoạt động: Instantiate prefab Fog P1/P2 và Runtime UI (1) nếu scene thiếu.
        /// </summary>
        static void EnsureMpPresentationPrerequisites()
        {
            EnsureFogPresentation(Owner.Player1, GameplayMapScenePaths.FogP1PrefabPath, "Fog of War P1");
            EnsureFogPresentation(Owner.Player2, GameplayMapScenePaths.FogP2PrefabPath, "Fog of War P2");
            EnsureSecondPlayerHudRoot();
        }

        static FactionFogPresentation EnsureFogPresentation(Owner owner, string prefabPath, string instanceName)
        {
            FactionFogPresentation existing = FindPresentation(owner);
            if (existing != null)
            {
                return existing;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[MpPresentation] Không tìm thấy {prefabPath} — thiếu fog {owner}.");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                prefab,
                SceneManager.GetActiveScene());
            instance.name = instanceName;
            FactionFogPresentation fog = instance.GetComponent<FactionFogPresentation>();
            if (owner == Owner.Player2 && fog != null)
            {
                FogOfWarCourseP2SetupEditor.ApplyCourseSection9ToPresentation(fog);
            }

            Debug.Log($"[MpPresentation] Đã thêm {instanceName} cho {owner}.");
            return fog;
        }

        /// <summary>
        /// Mục tiêu: P2 client có HUD riêng (Supplies + RuntimeUI bus Owner.Player2).
        /// Cách hoạt động: Nếu chưa có root tên chứa \"(1)\", instantiate Runtime UI UGUI prefab.
        /// </summary>
        static void EnsureSecondPlayerHudRoot()
        {
            Supplies playerTwoHud = FindHudSupplies(Owner.Player2);
            if (playerTwoHud != null && playerTwoHud.transform.root.name.Contains("(1)"))
            {
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                GameplayMapScenePaths.RuntimeUiHudPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"[MpPresentation] Không tìm thấy {GameplayMapScenePaths.RuntimeUiHudPrefabPath} — "
                    + "duplicate HUD P2 thủ công: Runtime UI UGUI (1).");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                prefab,
                SceneManager.GetActiveScene());
            instance.name = "Runtime UI UGUI (1)";
            Debug.Log("[MpPresentation] Đã thêm Runtime UI UGUI (1) cho HUD P2.");
        }

        static void LogPresentationValidation(
            FactionFogPresentation fogP1,
            FactionFogPresentation fogP2,
            Supplies hudP1,
            Supplies hudP2,
            MpPlayerPresentationRig rigP1,
            MpPlayerPresentationRig rigP2)
        {
            if (fogP1 == null || fogP2 == null)
            {
                Debug.LogWarning(
                    "[MpPresentation] Thiếu fog P1 hoặc P2 — chạy lại ProjectRTS/Gameplay/★ Prepare Open Scene As Gameplay Map.");
            }

            if (hudP1 == null || hudP2 == null)
            {
                Debug.LogWarning("[MpPresentation] Thiếu HUD Supplies P1 hoặc P2.");
            }
            else if (hudP1 == hudP2)
            {
                Debug.LogError(
                    "[MpPresentation] P1 và P2 đang dùng chung một HUD — MP client sẽ lỗi UI. "
                    + "Cần Runtime UI UGUI và Runtime UI UGUI (1).");
            }

            if (rigP1 == null || rigP2 == null)
            {
                Debug.LogWarning("[MpPresentation] Thiếu MpPlayerPresentationRig trên fog P1/P2.");
            }
            else
            {
                Debug.Log("[MpPresentation] MP presentation OK — fog x2, HUD x2, rigs wired.");
            }
        }

        const string MainCameraPrefabPath = "Assets/Prefab/Main Camera.prefab";

        /// <summary>
        /// Mục tiêu: Main Camera prefab có GameplayFogOverlayCamera — overlay layer 13 khớp zoom gameplay.
        /// </summary>
        public static void EnsureMainCameraFogOverlayPrefab()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(MainCameraPrefabPath);
            if (prefabRoot == null)
            {
                return;
            }

            try
            {
                Camera gameplayCam = prefabRoot.GetComponent<Camera>();
                Transform overlayTransform = prefabRoot.transform.Find("Fog of War Rendering Camera");
                Camera overlayCam = overlayTransform != null ? overlayTransform.GetComponent<Camera>() : null;

                GameplayFogOverlayCamera overlayBehaviour = prefabRoot.GetComponent<GameplayFogOverlayCamera>();
                if (overlayBehaviour == null)
                {
                    overlayBehaviour = prefabRoot.AddComponent<GameplayFogOverlayCamera>();
                }

                SerializedObject overlaySo = new SerializedObject(overlayBehaviour);
                overlaySo.FindProperty("fogOverlayCamera").objectReferenceValue = overlayCam;
                overlaySo.ApplyModifiedPropertiesWithoutUndo();

                if (overlayCam != null)
                {
                    overlayCam.cullingMask = 1 << OwnerFogPlaneLayers.DefaultPlayer1PlaneLayer;
                    overlayCam.clearFlags = CameraClearFlags.Depth;
                    overlayCam.depth = gameplayCam != null ? gameplayCam.depth + 1f : 0f;
                    EditorUtility.SetDirty(overlayCam);
                }

                FactionFogPresentation[] fogPresentations =
                    Object.FindObjectsByType<FactionFogPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < fogPresentations.Length; i++)
                {
                    fogPresentations[i].ApplyFogPlaneLayer();
                    EditorUtility.SetDirty(fogPresentations[i]);
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, MainCameraPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>
        /// Mục tiêu: Rig chỉ trên Fog P1/P2 — xóa rig trùng trên Main Camera gây fog/HUD lệch.
        /// </summary>
        static void RemoveRigFromMainCamera(PlayerInput mainInput)
        {
            if (mainInput == null)
            {
                return;
            }

            MpPlayerPresentationRig cameraRig = mainInput.GetComponent<MpPlayerPresentationRig>();
            if (cameraRig != null)
            {
                Object.DestroyImmediate(cameraRig);
            }
        }

        static MpPlayerPresentationRig EnsureRigOnFogRoot(
            Owner owner,
            FactionFogPresentation fog,
            PlayerInput input,
            Supplies supplies)
        {
            if (fog == null)
            {
                return null;
            }

            MpPlayerPresentationRig rig = fog.GetComponent<MpPlayerPresentationRig>();
            if (rig == null)
            {
                rig = fog.gameObject.AddComponent<MpPlayerPresentationRig>();
            }

            GameObject hudRoot = supplies != null ? supplies.transform.root.gameObject : null;

            SerializedObject so = new SerializedObject(rig);
            so.FindProperty("presentationOwner").intValue = (int)owner;
            so.FindProperty("rigRoot").objectReferenceValue = fog.gameObject;
            so.FindProperty("fogPresentation").objectReferenceValue = fog;
            so.FindProperty("playerInput").objectReferenceValue = input;
            so.FindProperty("suppliesHud").objectReferenceValue = supplies;
            so.FindProperty("hudRoot").objectReferenceValue = hudRoot;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rig);
            return rig;
        }

        static void ApplyFogCameraMasksInEditor(FactionFogPresentation fog, Owner owner)
        {
            if (fog == null)
            {
                return;
            }

            int layer = OwnerFogVisionLayers.GetLayer(owner);
            if (layer < 0)
            {
                return;
            }

            int mask = 1 << layer;
            SerializedObject so = new SerializedObject(fog);
            SerializedProperty explored = so.FindProperty("exploredFogCamera");
            SerializedProperty vision = so.FindProperty("visionFogCamera");

            FactionFogSystemReference systemRef = fog.FogSystemReference
                ?? fog.GetComponent<FactionFogSystemReference>();

            if (explored.objectReferenceValue is Camera exploredCam)
            {
                exploredCam.cullingMask = mask;
                if (systemRef != null && systemRef.ExploredRenderTexture != null)
                {
                    exploredCam.targetTexture = systemRef.ExploredRenderTexture;
                }

                EditorUtility.SetDirty(exploredCam);
            }

            if (vision.objectReferenceValue is Camera visionCam)
            {
                visionCam.cullingMask = mask;
                if (systemRef != null && systemRef.VisionRenderTexture != null)
                {
                    visionCam.targetTexture = systemRef.VisionRenderTexture;
                }

                EditorUtility.SetDirty(visionCam);
            }

            systemRef?.EnsureReferences();
            EditorUtility.SetDirty(fog);
        }

        static FactionVisibilityUpdater EnsureVisibilityUpdater(FactionFogPresentation fog)
        {
            if (fog == null)
            {
                return null;
            }

            FactionVisibilityUpdater updater = fog.GetComponentInChildren<FactionVisibilityUpdater>(true);
            if (updater != null)
            {
                return updater;
            }

            Camera visionCam = fog.VisionFogCamera;
            if (visionCam == null)
            {
                return null;
            }

            updater = visionCam.gameObject.GetComponent<FactionVisibilityUpdater>();
            if (updater == null)
            {
                updater = visionCam.gameObject.AddComponent<FactionVisibilityUpdater>();
            }

            SerializedObject so = new SerializedObject(updater);
            so.FindProperty("visionFogCamera").objectReferenceValue = visionCam;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(updater);
            return updater;
        }

        /// <summary>
        /// Mục tiêu: Lúc mở scene không hiện fog/HUD — Director bật khi có LocalOwner lúc Play.
        /// </summary>
        static void SetFogAndHudDefaultsInactive(
            FactionFogPresentation fogP1,
            FactionFogPresentation fogP2,
            Supplies hudP1,
            Supplies hudP2)
        {
            if (fogP1 != null)
            {
                fogP1.gameObject.SetActive(false);
            }

            if (fogP2 != null)
            {
                fogP2.gameObject.SetActive(false);
            }

            if (hudP1 != null)
            {
                hudP1.transform.root.gameObject.SetActive(false);
            }

            if (hudP2 != null)
            {
                hudP2.transform.root.gameObject.SetActive(false);
            }
        }

        static Supplies FindHudSupplies(Owner owner)
        {
            Supplies[] all = Object.FindObjectsByType<Supplies>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            Supplies fallback = null;
            for (int i = 0; i < all.Length; i++)
            {
                Supplies candidate = all[i];
                if (candidate == null)
                {
                    continue;
                }

                bool isDuplicateName = candidate.gameObject.name.Contains("(1)");
                if (owner == Owner.Player2)
                {
                    if (isDuplicateName)
                    {
                        return candidate;
                    }

                    fallback ??= candidate;
                }
                else if (!isDuplicateName)
                {
                    return candidate;
                }
            }

            return owner == Owner.Player2 ? fallback : null;
        }

        static void WireRuntimeUiBusOwners(Supplies hud, Owner owner)
        {
            if (hud == null)
            {
                return;
            }

            RuntimeUI[] runtimeUis = hud.GetComponentsInChildren<RuntimeUI>(true);
            for (int i = 0; i < runtimeUis.Length; i++)
            {
                runtimeUis[i].ConfigureBusOwner(owner);
                EditorUtility.SetDirty(runtimeUis[i]);
            }
        }

        static void WireHudBinderSupplies(Supplies hudP1)
        {
            FactionHudBinder binder = Object.FindFirstObjectByType<FactionHudBinder>(FindObjectsInactive.Include);
            if (binder == null || hudP1 == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(binder);
            so.FindProperty("supplies").objectReferenceValue = hudP1;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(binder);
        }

        static void EnsureLocalHumanOwnerOnMainCamera(PlayerInput mainInput)
        {
            if (mainInput == null)
            {
                if (Object.FindFirstObjectByType<LocalHumanOwnerService>(FindObjectsInactive.Include) == null)
                {
                    new GameObject("LocalHumanOwnerService").AddComponent<LocalHumanOwnerService>();
                }

                return;
            }

            if (mainInput.GetComponent<LocalHumanOwnerService>() == null)
            {
                mainInput.gameObject.AddComponent<LocalHumanOwnerService>();
            }
        }

        static void DisableLegacyPlayerViewBinder()
        {
            PlayerViewBinder legacyBinder = Object.FindFirstObjectByType<PlayerViewBinder>(FindObjectsInactive.Include);
            if (legacyBinder != null)
            {
                legacyBinder.enabled = false;
                EditorUtility.SetDirty(legacyBinder);
            }
        }

        static FactionFogPresentation FindPresentation(Owner owner)
        {
            FactionFogPresentation[] all = Object.FindObjectsByType<FactionFogPresentation>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].PresentationOwner == owner)
                {
                    return all[i];
                }
            }

            return null;
        }

        static PlayerInput FindMainCameraPlayerInput() =>
            Object.FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Include);
    }
}
#endif

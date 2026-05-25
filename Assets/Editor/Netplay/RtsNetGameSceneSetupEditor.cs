#if UNITY_EDITOR
using GameDevTV.RTS.AI;
using GameDevTV.RTS.Editor.FogOfWar;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.Units;
using Mirror;
using ProjectRTS.Netplay;
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
        const string GameScenePath = "Assets/3rdParty/RTS_Multiplayer/Scenes/RtsNet_Game.unity";
        const string FogP2PrefabPath = "Assets/Prefab/Fog of War P2.prefab";
        const string CivilCentralPath = "Assets/Prefab/Buildings/civil_central/civil_central.prefab";
        const string WorkerPath = "Assets/Prefab/Unit/Worker 1.prefab";

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
            EnsureSpawnAndGameplaySetup();
            MpPresentationSceneSetupEditor.WireInOpenScene();
            EnsureUtsPrefabNetworkComponents();
            DisableOfflineBootstrap();
            DisableAiForMp();
            RemoveScenePlacedNetworkPrefabs();
        }

        /// <summary>
        /// Mục tiêu: RtsUtsGameSceneSetup + spawn points + prefab CC/Worker trên scene.
        /// </summary>
        static void EnsureSpawnAndGameplaySetup()
        {
            Transform team0 = FindOrCreateSpawn("Team0Spawn", new Vector3(-185f, 0f, -174f));
            Transform team1 = FindOrCreateSpawn("Team1Spawn", new Vector3(185f, 0f, 174f));

            GameObject setupRoot = GameObject.Find("RtsUtsGameSceneSetup");
            if (setupRoot == null)
            {
                setupRoot = new GameObject("RtsUtsGameSceneSetup");
            }

            if (setupRoot.transform.parent == null)
            {
                team0.SetParent(setupRoot.transform);
                team1.SetParent(setupRoot.transform);
            }

            RtsUtsGameSceneSetup utsSetup = setupRoot.GetComponent<RtsUtsGameSceneSetup>();
            if (utsSetup == null)
            {
                utsSetup = setupRoot.AddComponent<RtsUtsGameSceneSetup>();
            }

            utsSetup.teamSpawnPoints = new[] { team0, team1 };
            utsSetup.civilCentralPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CivilCentralPath);
            utsSetup.startingWorkerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorkerPath);
            utsSetup.startingWorkerCount = 3;
            utsSetup.workerOffsetFromBase = new Vector3(10f, 0f, 10f);
            utsSetup.workerSpawnSpacing = new Vector3(10f, 0f, 0f);
            utsSetup.disableAiControllersOnLoad = true;
            EditorUtility.SetDirty(utsSetup);

            RtsGameSceneSetup legacySetup = setupRoot.GetComponent<RtsGameSceneSetup>();
            if (legacySetup == null)
            {
                legacySetup = setupRoot.AddComponent<RtsGameSceneSetup>();
            }

            legacySetup.teamSpawnPoints = new[] { team0, team1 };
            EditorUtility.SetDirty(legacySetup);

            if (Object.FindFirstObjectByType<RtsNetGameSceneBootstrap>(FindObjectsInactive.Include) == null)
            {
                setupRoot.AddComponent<RtsNetGameSceneBootstrap>();
            }

            if (setupRoot.GetComponent<RtsMatchServerSpawnRunner>() == null)
            {
                setupRoot.AddComponent<RtsMatchServerSpawnRunner>();
            }

            utsSetup.startingWorkerCount = Mathf.Max(utsSetup.startingWorkerCount, 3);
            EditorUtility.SetDirty(utsSetup);
        }

        static Transform FindOrCreateSpawn(string name, Vector3 localPosition)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                return existing.transform;
            }

            GameObject spawn = new GameObject(name);
            spawn.transform.localPosition = localPosition;
            return spawn.transform;
        }

        static void DisableOfflineBootstrap()
        {
            LocalHumanOwnerBootstrap[] bootstraps = Object.FindObjectsByType<LocalHumanOwnerBootstrap>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < bootstraps.Length; i++)
            {
                if (bootstraps[i] != null)
                {
                    bootstraps[i].enabled = false;
                }
            }
        }

        static void DisableAiForMp()
        {
            AIController[] controllers = Object.FindObjectsByType<AIController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null)
                {
                    controllers[i].enabled = false;
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Tránh lỗi Mirror sceneId trên CC/Worker đặt sẵn trong scene copy Game 1.
        /// </summary>
        /// <summary>
        /// Mục tiêu: civil_central + Worker 1 có RtsUtsNetworkEntity trên prefab (Mirror sync ổn định).
        /// </summary>
        static void EnsureUtsPrefabNetworkComponents()
        {
            EnsureNetworkEntityOnPrefab(CivilCentralPath);
            EnsureNetworkEntityOnPrefab(WorkerPath);
        }

        static void EnsureNetworkEntityOnPrefab(string assetPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[RtsNetGameScene] Không tìm thấy prefab: {assetPath}");
                return;
            }

            if (prefab.GetComponent<NetworkIdentity>() == null)
            {
                Debug.LogWarning($"[RtsNetGameScene] {prefab.name} thiếu NetworkIdentity.");
                return;
            }

            if (prefab.GetComponent<RtsUtsNetworkEntity>() == null)
            {
                prefab.AddComponent<RtsUtsNetworkEntity>();
                EditorUtility.SetDirty(prefab);
                Debug.Log($"[RtsNetGameScene] Đã thêm RtsUtsNetworkEntity → {prefab.name}");
            }
        }

        static void RemoveScenePlacedNetworkPrefabs()
        {
            var paths = new System.Collections.Generic.HashSet<string>
            {
                CivilCentralPath,
                WorkerPath,
            };

            NetworkIdentity[] identities = Object.FindObjectsByType<NetworkIdentity>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < identities.Length; i++)
            {
                NetworkIdentity identity = identities[i];
                if (identity == null || identity.gameObject.scene.path != GameScenePath)
                {
                    continue;
                }

                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(identity.gameObject);
                if (source == null)
                {
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(source);
                if (!paths.Contains(assetPath))
                {
                    continue;
                }

                Object.DestroyImmediate(identity.transform.root.gameObject);
            }
        }
    }

    /// <summary>
    /// Wire presentation — gọi từ Prepare scene.
    /// </summary>
    public static class MpPresentationSceneSetupEditor
    {
        const string GameScenePath = "Assets/3rdParty/RTS_Multiplayer/Scenes/RtsNet_Game.unity";
        const string FogP2PrefabPath = "Assets/Prefab/Fog of War P2.prefab";

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

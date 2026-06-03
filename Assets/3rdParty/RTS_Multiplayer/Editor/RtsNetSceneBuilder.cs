#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mirror;
using ProjectRTS.Netplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectRTS.Netplay.Editor
{
    /// <summary>
    /// Mục tiêu: Sinh prefab + scene lobby/game + Build Settings một lần để tránh chỉnh YAML tay.
    /// Cách hoạt động: Menu tạo hierarchy uGUI, NetworkManager, Telepathy, Authenticator, gán prefab và spawnables.
    /// </summary>
    public static class RtsNetSceneBuilder
    {
        static Font GetUiFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null)
                f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        const string Root = "Assets/3rdParty/RTS_Multiplayer";
        const string PrefabDir = Root + "/Prefabs";
        const string SceneDir = Root + "/Scenes";
        const string LobbyScenePath = SceneDir + "/RtsNet_Lobby.unity";
        const string GameScenePath = SceneDir + "/RtsNet_Game.unity";
        const string PlayerPrefabPath = PrefabDir + "/RtsNet_Player.prefab";
        const string UnitPrefabPath = PrefabDir + "/RtsNet_Unit.prefab";

        [MenuItem("ProjectRTS/Netplay/Generate RTS Mirror scenes & prefabs")]
        public static void Generate()
        {
            EnsureFolder(Root);
            EnsureFolder(PrefabDir);
            EnsureFolder(SceneDir);

            PlayerSettings.runInBackground = true;

            GameObject unitTemp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unitTemp.name = "RtsNet_Unit";
            unitTemp.transform.localScale = new Vector3(0.9f, 0.45f, 0.9f);
            Object.DestroyImmediate(unitTemp.GetComponent<CapsuleCollider>());
            var unitCol = unitTemp.AddComponent<BoxCollider>();
            unitCol.size = new Vector3(0.6f, 0.9f, 0.6f);
            unitCol.center = Vector3.zero;

            var unitNi = unitTemp.AddComponent<NetworkIdentity>();
            unitTemp.AddComponent<NetworkTransformUnreliable>();
            unitTemp.AddComponent<RtsUnit>();

            GameObject unitPrefabAsset = PrefabUtility.SaveAsPrefabAsset(unitTemp, UnitPrefabPath);
            Object.DestroyImmediate(unitTemp);

            GameObject playerTemp = new GameObject("RtsNet_Player");
            playerTemp.AddComponent<NetworkIdentity>();
            playerTemp.AddComponent<RtsLobbyPlayer>();
            playerTemp.AddComponent<RtsGameCommander>();
            playerTemp.AddComponent<RtsGameInput>();
            playerTemp.AddComponent<RtsPlayerEconomy>();
            GameObject playerPrefabAsset = PrefabUtility.SaveAsPrefabAsset(playerTemp, PlayerPrefabPath);
            Object.DestroyImmediate(playerTemp);

            var unitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitPrefabPath);
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            BuildLobbyScene(playerPrefab, unitPrefab);
            BuildGameScene();

            var newScenes = new[]
            {
                new EditorBuildSettingsScene(LobbyScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };
            var merged = new List<EditorBuildSettingsScene>();
            var seen = new HashSet<string>();
            foreach (var s in newScenes)
            {
                merged.Add(s);
                seen.Add(s.path);
            }
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s != null && !seen.Contains(s.path))
                {
                    merged.Add(s);
                    seen.Add(s.path);
                }
            }
            EditorBuildSettings.scenes = merged.ToArray();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RtsNetSceneBuilder] Xong. Mở scene RtsNet_Lobby và bấm Play (host) + build client.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        static void BuildLobbyScene(GameObject playerPrefab, GameObject unitPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "RtsNet_Lobby";

            var netGo = new GameObject("NetworkManager");
            var tp = netGo.AddComponent<TelepathyTransport>();
            var auth = netGo.AddComponent<RtsUniqueNameAuthenticator>();
            var nm = netGo.AddComponent<RtsNetworkManager>();
            nm.authenticator = auth;
            nm.transport = tp;
            nm.playerPrefab = playerPrefab;
            nm.unitPrefab = unitPrefab;
            nm.lobbyScene = "SSScene";
            nm.gameScene = "Game 1";
            nm.maxConnections = 2;
            nm.networkAddress = "localhost";
            nm.offlineScene = "SSScene";
            nm.onlineScene = "";
            nm.spawnPrefabs.Add(unitPrefab);

            var startA = new GameObject("StartPos_Team0");
            startA.transform.position = new Vector3(-2f, 0f, 0f);
            startA.AddComponent<NetworkStartPosition>();
            var startB = new GameObject("StartPos_Team1");
            startB.transform.position = new Vector3(2f, 0f, 0f);
            startB.AddComponent<NetworkStartPosition>();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var mpPanel = CreatePanel(canvasGo.transform, "MP Panel", out RectTransform mpRt);
            mpRt.anchorMin = new Vector2(0.1f, 0.1f);
            mpRt.anchorMax = new Vector2(0.9f, 0.9f);
            mpRt.offsetMin = Vector2.zero;
            mpRt.offsetMax = Vector2.zero;

            Transform roomListContent = CreateScrollViewContent(mpPanel.transform);

            var readyBtn = CreateButton(mpPanel.transform, "Button Ready", "Ready");
            var readyRt = readyBtn.GetComponent<RectTransform>();
            readyRt.anchorMin = new Vector2(0.1f, 0.04f);
            readyRt.anchorMax = new Vector2(0.45f, 0.12f);
            readyRt.offsetMin = Vector2.zero;
            readyRt.offsetMax = Vector2.zero;

            var startBtn = CreateButton(mpPanel.transform, "Button Start Game", "Bắt đầu trận");
            var startRt = startBtn.GetComponent<RectTransform>();
            startRt.anchorMin = new Vector2(0.55f, 0.04f);
            startRt.anchorMax = new Vector2(0.9f, 0.12f);
            startRt.offsetMin = Vector2.zero;
            startRt.offsetMax = Vector2.zero;

            var ui = mpPanel.AddComponent<RtsLobbyUI>();
            SerializedObject lobbySerialized = new SerializedObject(ui);
            lobbySerialized.FindProperty("roomListContent").objectReferenceValue = roomListContent;
            lobbySerialized.FindProperty("readyButton").objectReferenceValue = readyBtn;
            lobbySerialized.FindProperty("startGameButton").objectReferenceValue = startBtn;
            lobbySerialized.FindProperty("authenticator").objectReferenceValue = auth;
            lobbySerialized.FindProperty("readyButtonImage").objectReferenceValue = readyBtn.GetComponent<Image>();
            lobbySerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, LobbyScenePath);
        }

        static Transform CreateScrollViewContent(Transform parent)
        {
            var scrollGo = new GameObject("Scroll View");
            scrollGo.transform.SetParent(parent, false);
            var scrollRt = scrollGo.AddComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.05f, 0.16f);
            scrollRt.anchorMax = new Vector2(0.95f, 0.95f);
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            viewportGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);
            viewportGo.AddComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0f, 0f);
            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 8f;
            contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRt;
            scrollRect.content = contentRt;
            return contentGo.transform;
        }

        static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "RtsNet_Game";

            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Ground";
            plane.transform.localScale = new Vector3(4f, 1f, 4f);
            plane.layer = LayerMask.NameToLayer("Default");

            var setupGo = new GameObject("RtsGameSceneSetup");
            var setup = setupGo.AddComponent<RtsGameSceneSetup>();
            var t0 = new GameObject("Team0Spawn").transform;
            t0.SetParent(setupGo.transform);
            t0.position = new Vector3(-6f, 0.5f, 0f);
            var t1 = new GameObject("Team1Spawn").transform;
            t1.SetParent(setupGo.transform);
            t1.position = new Vector3(6f, 0.5f, 0f);
            setup.teamSpawnPoints = new[] { t0, t1 };

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 18f, -14f);
            cam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

            var light = new GameObject("Directional Light");
            var l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            var canvasGo = new GameObject("HudCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var goldText = CreateText(canvasGo.transform, "GoldText", "Vàng: 0", 20, TextAnchor.UpperLeft);
            var grt = goldText.rectTransform;
            grt.anchorMin = new Vector2(0f, 1f);
            grt.anchorMax = new Vector2(0.4f, 1f);
            grt.pivot = new Vector2(0f, 1f);
            grt.anchoredPosition = new Vector2(12f, -12f);

            var hud = canvasGo.AddComponent<RtsGoldHud>();
            hud.EditorAssignGoldLabel(goldText);

            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        static GameObject CreatePanel(Transform parent, string name, out RectTransform rt)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            rt = go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.15f, 0.92f);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        static InputField CreateInputField(Transform parent, string name, string placeholder)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rt = root.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 36f);
            var img = root.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            var field = root.AddComponent<InputField>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(root.transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = GetUiFont();
            text.color = Color.white;
            text.supportRichText = true;
            var trt = text.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(6f, 2f);
            trt.offsetMax = new Vector2(-6f, -2f);
            field.textComponent = text;

            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(root.transform, false);
            var ph = phGo.AddComponent<Text>();
            ph.font = GetUiFont();
            ph.color = new Color(1f, 1f, 1f, 0.45f);
            ph.text = placeholder;
            var prt = ph.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(6f, 2f);
            prt.offsetMax = new Vector2(-6f, -2f);
            field.placeholder = ph;

            return field;
        }

        static Button CreateButton(Transform parent, string name, string label)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rt = root.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120f, 36f);
            var img = root.AddComponent<Image>();
            img.color = new Color(0.25f, 0.45f, 0.75f, 1f);
            var btn = root.AddComponent<Button>();
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(root.transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = GetUiFont();
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            var trt = text.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            return btn;
        }

        static Text CreateText(Transform parent, string name, string content, int size, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400f, 200f);
            var t = go.AddComponent<Text>();
            t.font = GetUiFont();
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.supportRichText = true;
            t.text = content;
            return t;
        }
    }
}
#endif

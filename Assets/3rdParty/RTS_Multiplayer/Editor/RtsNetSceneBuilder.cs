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
            nm.lobbyScene = "RtsNet_Lobby";
            nm.gameScene = "RtsNet_Game";
            nm.maxConnections = 2;
            nm.networkAddress = "localhost";
            nm.offlineScene = "RtsNet_Lobby";
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

            var loginPanel = CreatePanel(canvasGo.transform, "LoginPanel", out RectTransform loginRt);
            loginRt.anchorMin = new Vector2(0.35f, 0.35f);
            loginRt.anchorMax = new Vector2(0.65f, 0.65f);
            loginRt.offsetMin = Vector2.zero;
            loginRt.offsetMax = Vector2.zero;

            var addr = CreateInputField(loginPanel.transform, "AddressInput", "127.0.0.1");
            var user = CreateInputField(loginPanel.transform, "UsernameInput", "Player");
            var hostBtn = CreateButton(loginPanel.transform, "HostButton", "Host");
            var clientBtn = CreateButton(loginPanel.transform, "ClientButton", "Client");

            var lobbyPanel = CreatePanel(canvasGo.transform, "LobbyPanel", out RectTransform lobbyRt);
            lobbyRt.anchorMin = new Vector2(0.1f, 0.1f);
            lobbyRt.anchorMax = new Vector2(0.9f, 0.9f);
            lobbyRt.offsetMin = Vector2.zero;
            lobbyRt.offsetMax = Vector2.zero;
            lobbyPanel.SetActive(false);

            var chatHistory = CreateText(lobbyPanel.transform, "ChatHistory", "", 14, TextAnchor.LowerLeft);
            var chatRt = chatHistory.rectTransform;
            chatRt.anchorMin = new Vector2(0f, 0.15f);
            chatRt.anchorMax = new Vector2(1f, 1f);
            chatRt.offsetMin = new Vector2(8f, 8f);
            chatRt.offsetMax = new Vector2(-8f, -8f);

            var chatInput = CreateInputField(lobbyPanel.transform, "ChatInput", "");
            var chatInputRt = chatInput.GetComponent<RectTransform>();
            chatInputRt.anchorMin = new Vector2(0f, 0f);
            chatInputRt.anchorMax = new Vector2(0.75f, 0.12f);
            chatInputRt.offsetMin = new Vector2(8f, 8f);
            chatInputRt.offsetMax = new Vector2(-4f, -4f);

            var sendBtn = CreateButton(lobbyPanel.transform, "SendChat", "Gửi");
            var sendRt = sendBtn.GetComponent<RectTransform>();
            sendRt.anchorMin = new Vector2(0.76f, 0f);
            sendRt.anchorMax = new Vector2(1f, 0.12f);
            sendRt.offsetMin = new Vector2(4f, 8f);
            sendRt.offsetMax = new Vector2(-8f, -4f);

            var readyBtn = CreateButton(lobbyPanel.transform, "ReadyButton", "Ready");
            var readyRt = readyBtn.GetComponent<RectTransform>();
            readyRt.anchorMin = new Vector2(0.35f, 0.12f);
            readyRt.anchorMax = new Vector2(0.5f, 0.18f);
            readyRt.offsetMin = Vector2.zero;
            readyRt.offsetMax = Vector2.zero;

            var startBtn = CreateButton(lobbyPanel.transform, "StartGameButton", "Bắt đầu trận (Host)");
            var startRt = startBtn.GetComponent<RectTransform>();
            startRt.anchorMin = new Vector2(0.52f, 0.12f);
            startRt.anchorMax = new Vector2(0.85f, 0.18f);
            startRt.offsetMin = Vector2.zero;
            startRt.offsetMax = Vector2.zero;

            var status = CreateText(lobbyPanel.transform, "StatusText", "Lobby", 16, TextAnchor.UpperLeft);
            var stRt = status.rectTransform;
            stRt.anchorMin = new Vector2(0f, 0.78f);
            stRt.anchorMax = new Vector2(1f, 1f);
            stRt.offsetMin = new Vector2(8f, -8f);
            stRt.offsetMax = new Vector2(-8f, -4f);

            var scroll = new GameObject("Scrollbar");
            scroll.transform.SetParent(lobbyPanel.transform, false);
            var sb = scroll.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            var sbRt = scroll.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(1f, 0.15f);
            sbRt.anchorMax = new Vector2(1f, 1f);
            sbRt.offsetMin = new Vector2(-24f, 8f);
            sbRt.offsetMax = new Vector2(-4f, -8f);

            var chatGo = new GameObject("LobbyChat");
            chatGo.AddComponent<NetworkIdentity>();
            var lobbyChat = chatGo.AddComponent<RtsLobbyChat>();
            lobbyChat.EditorAssignUi(chatHistory, sb, chatInput, sendBtn);

            var ui = canvasGo.AddComponent<RtsLobbyUI>();
            ui.EditorAssignUi(addr, user, hostBtn, clientBtn, readyBtn, startBtn, status, auth, loginPanel, lobbyPanel);

            hostBtn.onClick.AddListener(ui.OnClickHost);
            clientBtn.onClick.AddListener(ui.OnClickClient);
            readyBtn.onClick.AddListener(ui.OnClickReady);
            startBtn.onClick.AddListener(ui.OnClickStartGame);
            sendBtn.onClick.AddListener(lobbyChat.UiSendMessage);
            chatInput.onValueChanged.AddListener(lobbyChat.UiOnMessageChanged);
            chatInput.onEndEdit.AddListener(lobbyChat.UiOnEndEdit);

            var addrRt = addr.GetComponent<RectTransform>();
            addrRt.anchorMin = new Vector2(0f, 0.72f);
            addrRt.anchorMax = new Vector2(1f, 0.88f);
            addrRt.offsetMin = new Vector2(8f, 0f);
            addrRt.offsetMax = new Vector2(-8f, 0f);
            var userRt = user.GetComponent<RectTransform>();
            userRt.anchorMin = new Vector2(0f, 0.52f);
            userRt.anchorMax = new Vector2(1f, 0.68f);
            userRt.offsetMin = new Vector2(8f, 0f);
            userRt.offsetMax = new Vector2(-8f, 0f);
            var hRt = hostBtn.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 0.32f);
            hRt.anchorMax = new Vector2(0.48f, 0.46f);
            hRt.offsetMin = new Vector2(8f, 0f);
            hRt.offsetMax = new Vector2(-4f, 0f);
            var cRt = clientBtn.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.52f, 0.32f);
            cRt.anchorMax = new Vector2(1f, 0.46f);
            cRt.offsetMin = new Vector2(4f, 0f);
            cRt.offsetMax = new Vector2(-8f, 0f);

            EditorSceneManager.SaveScene(scene, LobbyScenePath);
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

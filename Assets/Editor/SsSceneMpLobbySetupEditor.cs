#if UNITY_EDITOR
using System;
using GameDevTV.RTS.UI.Pregame;
using ProjectRTS.Netplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.EditorTools
{
    /// <summary>
    /// Wire RtsLobbyUI mới trên SSScene (scrollview phòng, Ready/Start, authenticator).
    /// </summary>
    public static class SsSceneMpLobbySetupEditor
    {
        const string SetupScenePath = "Assets/Scenes/SSScene.unity";

        [MenuItem("ProjectRTS/Pregame/Wire SSScene MP Lobby UI")]
        public static void WireMpLobbyUi()
        {
            if (!EditorSceneManager.GetActiveScene().path.Replace('\\', '/').EndsWith("SSScene.unity"))
            {
                EditorSceneManager.OpenScene(SetupScenePath);
            }

            GameObject mpPanel = FindDeepChild(UnityEngine.Object.FindFirstObjectByType<Canvas>()?.transform, "MP Panel")?.gameObject;
            if (mpPanel == null)
            {
                Debug.LogError("[SsSceneMpLobbySetup] Không tìm thấy MP Panel.");
                return;
            }

            RtsLobbyUI lobbyUi = mpPanel.GetComponent<RtsLobbyUI>();
            if (lobbyUi == null)
            {
                lobbyUi = mpPanel.AddComponent<RtsLobbyUI>();
            }

            Transform content = ResolveMpRoomListContent(mpPanel.transform);
            RemoveLegacyRoomButtons(content);

            Button readyButton = FindDeepChild(mpPanel.transform, "Button Ready")?.GetComponent<Button>();
            Button startButton = FindDeepChild(mpPanel.transform, "Button Start Game")?.GetComponent<Button>();
            if (startButton == null)
            {
                startButton = FindDeepChild(mpPanel.transform, "Button Start (2)")?.GetComponent<Button>();
            }

            RtsUniqueNameAuthenticator authenticator =
                UnityEngine.Object.FindFirstObjectByType<RtsUniqueNameAuthenticator>(FindObjectsInactive.Include);
            GameObject readyHoverPanel = ResolveReadyHoverPanel(readyButton);

            SerializedObject lobbySerialized = new SerializedObject(lobbyUi);
            lobbySerialized.FindProperty("roomListContent").objectReferenceValue = content;
            lobbySerialized.FindProperty("readyButton").objectReferenceValue = readyButton;
            lobbySerialized.FindProperty("startGameButton").objectReferenceValue = startButton;
            lobbySerialized.FindProperty("authenticator").objectReferenceValue = authenticator;
            lobbySerialized.FindProperty("readyHoverPanel").objectReferenceValue = readyHoverPanel;
            if (readyButton != null)
            {
                lobbySerialized.FindProperty("readyButtonImage").objectReferenceValue = readyButton.GetComponent<Image>();
            }

            if (startButton != null)
            {
                lobbySerialized.FindProperty("startGameButtonImage").objectReferenceValue = startButton.GetComponent<Image>();
            }

            lobbySerialized.ApplyModifiedPropertiesWithoutUndo();

            PregameSetupUIController setup = UnityEngine.Object.FindFirstObjectByType<PregameSetupUIController>();
            if (setup != null)
            {
                SerializedObject setupSerialized = new SerializedObject(setup);
                setupSerialized.FindProperty("lobbyUi").objectReferenceValue = lobbyUi;
                setupSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(setup);
            }

            RtsNetworkManager networkManager = UnityEngine.Object.FindFirstObjectByType<RtsNetworkManager>(FindObjectsInactive.Include);
            if (networkManager != null)
            {
                SerializedObject nmSerialized = new SerializedObject(networkManager);
                nmSerialized.FindProperty("lobbyScene").stringValue = "SSScene";
                nmSerialized.FindProperty("offlineScene").stringValue = "SSScene";
                nmSerialized.FindProperty("gameScene").stringValue = "Game 1";
                nmSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(networkManager);
            }

            EditorUtility.SetDirty(lobbyUi);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[SsSceneMpLobbySetup] Đã wire MP Lobby UI trên SSScene (đã xóa Button_Room cũ nếu có).");
        }

        static Transform ResolveMpRoomListContent(Transform mpPanel)
        {
            ScrollRect scrollRect = mpPanel.GetComponentInChildren<ScrollRect>(true);
            if (scrollRect != null && scrollRect.content != null)
            {
                return scrollRect.content;
            }

            return FindDeepChild(mpPanel, "Content");
        }

        static void RemoveLegacyRoomButtons(Transform content)
        {
            if (content == null)
            {
                return;
            }

            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (child.name.StartsWith("RoomEntry_", StringComparison.Ordinal))
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        static GameObject ResolveReadyHoverPanel(Button readyButton)
        {
            if (readyButton == null)
            {
                return null;
            }

            Transform hoverChild = readyButton.transform.Find("Hover Panel");
            if (hoverChild == null)
            {
                hoverChild = FindDeepChild(readyButton.transform, "Panel");
            }

            if (hoverChild == null || hoverChild.gameObject == readyButton.gameObject)
            {
                return null;
            }

            return hoverChild.gameObject;
        }

        static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent.name == name)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeepChild(parent.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
#endif

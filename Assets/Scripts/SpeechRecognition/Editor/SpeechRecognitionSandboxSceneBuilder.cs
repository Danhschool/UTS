#if UNITY_EDITOR
using ProjectRTS.SpeechRecognition;
using ProjectRTS.SpeechRecognition.Core;
using ProjectRTS.SpeechRecognition.Whisper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Whisper;
using Whisper.Utils;

namespace ProjectRTS.SpeechRecognition.Editor
{
    /// <summary>
    /// Tạo scene test STT (Whisper) — không gắn map lệnh / gameplay voice.
    /// </summary>
    public static class SpeechRecognitionSandboxSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SpeechRecognition_Sandbox.unity";

        [MenuItem("ProjectRTS/Speech/Create STT Test Scene (Whisper only)")]
        public static void CreateSttOnlyTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            CreateSttOnlyStack(out var driver, out var controls);
            CreateSttOnlyUi(driver, controls);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[STT] Scene: {ScenePath}\n" +
                $"Model: StreamingAssets/{WhisperSpeechDefaults.BaseModelRelativePath}\n" +
                "Play → nhấn nút thu (bật/tắt) — chỉ hiện text khi đang thu.",
                null);
        }

        [MenuItem("ProjectRTS/Speech/Add STT Only To Active Scene")]
        public static void AddSttOnlyToActiveScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("Không có scene đang mở.", null);
                return;
            }

            if (Object.FindFirstObjectByType<WhisperSttOnlyDriver>() != null)
            {
                Debug.LogWarning("Scene đã có WhisperSttOnlyDriver.", null);
                return;
            }

            CreateSttOnlyStack(out var driver, out var controls);
            CreateSttOnlyUi(driver, controls);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[STT] Đã thêm Whisper STT + nút thu/dừng vào '{scene.name}'.", null);
        }

        [MenuItem("ProjectRTS/Speech/Add STT + Chat To SpeechRecognition Prefab")]
        public static void AddSttChatToPrefab()
        {
            const string prefabPath = "Assets/Prefab/SpeechRecognition.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogError($"Không mở được prefab: {prefabPath}", null);
                return;
            }

            if (root.GetComponent<VoiceCommandOneShotCapture>() == null)
            {
                root.AddComponent<VoiceCommandOneShotCapture>();
            }

            if (root.GetComponent<VoiceCommandPushToTalkInput>() == null)
            {
                root.AddComponent<VoiceCommandPushToTalkInput>();
            }

            if (root.GetComponent<WhisperSttRecordControls>() == null)
            {
                root.AddComponent<WhisperSttRecordControls>();
            }

            if (root.GetComponent<VoiceCommandRuntimeDiagnostics>() == null)
            {
                root.AddComponent<VoiceCommandRuntimeDiagnostics>();
            }

            var driver = root.GetComponent<WhisperSttOnlyDriver>();
            if (driver != null)
            {
                var driverSo = new SerializedObject(driver);
                driverSo.FindProperty("_autoStartOnPlay").boolValue = false;
                driverSo.FindProperty("_ensureRecordControls").boolValue = false;
                driverSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[STT] Đã gắn STT + chat (phím V) vào {prefabPath}.", null);
        }

        [MenuItem("ProjectRTS/Speech/Add Record Controls To SpeechRecognition Prefab")]
        public static void AddControlsToSpeechRecognitionPrefab()
        {
            const string prefabPath = "Assets/Prefab/SpeechRecognition.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogError($"Không mở được prefab: {prefabPath}", null);
                return;
            }

            var driver = root.GetComponent<WhisperSttOnlyDriver>();
            if (driver == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.LogError("Prefab thiếu WhisperSttOnlyDriver.", null);
                return;
            }

            if (root.GetComponent<WhisperSttRecordControls>() == null)
            {
                root.AddComponent<WhisperSttRecordControls>();
            }

            var driverSo = new SerializedObject(driver);
            driverSo.FindProperty("_autoStartOnPlay").boolValue = false;
            driverSo.FindProperty("_ensureRecordControls").boolValue = true;
            driverSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[STT] Đã gắn {nameof(WhisperSttRecordControls)} vào {prefabPath}. Play → nút bật/tắt thu.", null);
        }

        [MenuItem("ProjectRTS/Speech/Add STT + Chat To Active Scene")]
        public static void AddSttChatToActiveScene()
        {
            var driver = Object.FindFirstObjectByType<WhisperSttOnlyDriver>();
            if (driver == null)
            {
                Debug.LogError("Scene chưa có WhisperSttOnlyDriver.", null);
                return;
            }

            var go = driver.gameObject;
            if (go.GetComponent<VoiceCommandOneShotCapture>() == null)
            {
                go.AddComponent<VoiceCommandOneShotCapture>();
            }

            if (go.GetComponent<VoiceCommandPushToTalkInput>() == null)
            {
                go.AddComponent<VoiceCommandPushToTalkInput>();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[STT] Đã thêm STT + chat. Play → nhấn V.", driver);
        }

        [MenuItem("ProjectRTS/Speech/Add STT Record Controls To Active Scene")]
        public static void AddSttControlsToActiveScene()
        {
            var driver = Object.FindFirstObjectByType<WhisperSttOnlyDriver>();
            if (driver == null)
            {
                Debug.LogError("Scene chưa có WhisperSttOnlyDriver — dùng Add STT Only To Active Scene trước.", null);
                return;
            }

            if (driver.GetComponent<WhisperSttRecordControls>() != null)
            {
                Debug.LogWarning("Đã có WhisperSttRecordControls.", driver);
                return;
            }

            var controls = driver.gameObject.AddComponent<WhisperSttRecordControls>();
            var driverSo = new SerializedObject(driver);
            driverSo.FindProperty("_autoStartOnPlay").boolValue = false;
            driverSo.ApplyModifiedPropertiesWithoutUndo();

            CreateSttOnlyUi(driver, controls);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[STT] Đã thêm UI Bắt đầu/Dừng thu.", driver);
        }

        private static void CreateSttOnlyStack(out WhisperSttOnlyDriver driver, out WhisperSttRecordControls controls)
        {
            var root = new GameObject("SpeechRecognition");
            var whisper = root.AddComponent<WhisperManager>();
            var mic = root.AddComponent<MicrophoneRecord>();
            driver = root.AddComponent<WhisperSttOnlyDriver>();
            controls = root.AddComponent<WhisperSttRecordControls>();

            ConfigureWhisperManager(whisper);
            ConfigureMicrophone(mic);

            var driverSo = new SerializedObject(driver);
            driverSo.FindProperty("_whisper").objectReferenceValue = whisper;
            driverSo.FindProperty("_microphoneRecord").objectReferenceValue = mic;
            driverSo.FindProperty("_autoStartOnPlay").boolValue = false;
            driverSo.FindProperty("_latencyProfile").enumValueIndex = (int)WhisperSttLatencyProfile.LowLatency;
            driverSo.ApplyModifiedPropertiesWithoutUndo();

            var controlsSo = new SerializedObject(controls);
            controlsSo.FindProperty("_driver").objectReferenceValue = driver;
            controlsSo.FindProperty("_createRuntimeUiIfMissing").boolValue = false;
            controlsSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureWhisperManager(WhisperManager manager)
        {
            var so = new SerializedObject(manager);
            so.FindProperty("modelPath").stringValue = WhisperSpeechDefaults.BaseModelRelativePath;
            so.FindProperty("isModelPathInStreamingAssets").boolValue = true;
            so.FindProperty("initOnAwake").boolValue = true;
            so.FindProperty("language").stringValue = WhisperSpeechDefaults.DefaultLanguage;
            so.FindProperty("translateToEnglish").boolValue = false;
            so.FindProperty("stepSec").floatValue = WhisperStreamingTuning.LowLatencyStepSec;
            so.FindProperty("keepSec").floatValue = WhisperStreamingTuning.LowLatencyKeepSec;
            so.FindProperty("lengthSec").floatValue = WhisperStreamingTuning.LowLatencyLengthSec;
            so.FindProperty("dropOldBuffer").boolValue = true;
            so.FindProperty("updatePrompt").boolValue = false;
            so.FindProperty("singleSegment").boolValue = true;
            so.FindProperty("useVad").boolValue = true;
            so.FindProperty("noContext").boolValue = true;
            so.FindProperty("initialPrompt").stringValue = string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureMicrophone(MicrophoneRecord microphone)
        {
            var so = new SerializedObject(microphone);
            so.FindProperty("frequency").intValue = 16000;
            so.FindProperty("chunksLengthSec").floatValue = WhisperStreamingTuning.LowLatencyMicChunkSec;
            so.FindProperty("loop").boolValue = true;
            so.FindProperty("useVad").boolValue = true;
            so.FindProperty("vadLastSec").floatValue = WhisperStreamingTuning.LowLatencyVadLastSec;
            so.FindProperty("vadStop").boolValue = false;
            so.FindProperty("echo").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateSttOnlyUi(WhisperSttOnlyDriver driver, WhisperSttRecordControls controls)
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var canvasGo = new GameObject("SttUI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGo.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.06f, 0.08f, 0.12f, 0.9f);

            var transcriptGo = new GameObject("Transcript");
            transcriptGo.transform.SetParent(panel.transform, false);
            var rect = transcriptGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.05f, 0.14f);
            rect.anchorMax = new Vector2(0.95f, 0.86f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var transcript = transcriptGo.AddComponent<Text>();
            transcript.font = font;
            transcript.fontSize = 32;
            transcript.color = Color.white;
            transcript.alignment = TextAnchor.UpperLeft;
            transcript.horizontalOverflow = HorizontalWrapMode.Wrap;
            transcript.verticalOverflow = VerticalWrapMode.Overflow;
            transcript.text = "STT — nhấn «Bắt đầu thu».";

            var statusGo = new GameObject("Status");
            statusGo.transform.SetParent(panel.transform, false);
            var statusRect = statusGo.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.05f, 0.88f);
            statusRect.anchorMax = new Vector2(0.95f, 0.98f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            var statusText = statusGo.AddComponent<Text>();
            statusText.font = font;
            statusText.fontSize = 22;
            statusText.color = new Color(0.75f, 0.85f, 1f);
            statusText.text = "Đã dừng — nhấn Bắt đầu thu.";

            var toggleGo = new GameObject("ToggleRecord");
            toggleGo.transform.SetParent(panel.transform, false);
            var toggleRect = toggleGo.AddComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0.25f, 0.02f);
            toggleRect.anchorMax = new Vector2(0.75f, 0.12f);
            toggleRect.offsetMin = Vector2.zero;
            toggleRect.offsetMax = Vector2.zero;

            var toggleButton = CreateEditorButton(toggleGo.transform, font, "Bắt đầu thu",
                new Color(0.15f, 0.55f, 0.25f), Vector2.zero, Vector2.one);
            var toggleLabel = toggleButton.GetComponentInChildren<Text>();

            var driverSo = new SerializedObject(driver);
            driverSo.FindProperty("_transcriptText").objectReferenceValue = transcript;
            driverSo.ApplyModifiedPropertiesWithoutUndo();

            var controlsSo = new SerializedObject(controls);
            controlsSo.FindProperty("_driver").objectReferenceValue = driver;
            controlsSo.FindProperty("_toggleButton").objectReferenceValue = toggleButton;
            controlsSo.FindProperty("_toggleButtonLabel").objectReferenceValue = toggleLabel;
            controlsSo.FindProperty("_statusText").objectReferenceValue = statusText;
            controlsSo.FindProperty("_createRuntimeUiIfMissing").boolValue = false;
            controlsSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button CreateEditorButton(
            Transform parent,
            Font font,
            string label,
            Color bg,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.color = bg;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.font = font;
            text.fontSize = 26;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;

            return button;
        }
    }
}
#endif

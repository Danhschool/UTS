using GameDevTV.RTS.UI.GameEventLog;
using ProjectRTS.SpeechRecognition;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectRTS.SpeechRecognition.Whisper
{
    /// <summary>
    /// Một nút bật/tắt thu âm STT (SRP: chỉ điều khiển UI, không xử lý Whisper).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WhisperSttRecordControls : MonoBehaviour
    {
        private static readonly Color IdleButtonColor = new(0.15f, 0.55f, 0.25f);
        private static readonly Color RecordingButtonColor = new(0.65f, 0.18f, 0.15f);

        [SerializeField] private WhisperSttOnlyDriver _driver;
        [SerializeField] private VoiceCommandOneShotCapture _commandCapture;
        [SerializeField] private Button _toggleButton;
        [SerializeField] private Text _toggleButtonLabel;
        [SerializeField] private Text _statusText;

        [Tooltip("Nếu chưa gán nút, tự tạo Canvas + nút khi Play (tiện cho prefab SpeechRecognition).")]
        [SerializeField] private bool _createRuntimeUiIfMissing = true;

        private Image _toggleButtonImage;

        private void Awake()
        {
            if (_driver == null)
            {
                _driver = GetComponent<WhisperSttOnlyDriver>();
            }

            if (_driver == null)
            {
                _driver = GetComponentInParent<WhisperSttOnlyDriver>();
            }

            if (_commandCapture == null)
            {
                _commandCapture = GetComponent<VoiceCommandOneShotCapture>();
            }

            CacheToggleGraphic();

            if (_createRuntimeUiIfMissing && _toggleButton == null)
            {
                BuildRuntimeUi();
                CacheToggleGraphic();
            }
        }

        private void OnEnable()
        {
            if (_toggleButton != null)
            {
                _toggleButton.onClick.AddListener(OnToggleClicked);
            }

            if (_driver != null)
            {
                _driver.ListeningStateChanged += OnListeningStateChanged;
                RefreshToggleUi(_driver.IsListening);
            }

            if (_commandCapture != null)
            {
                _commandCapture.TranscriptCommitted += OnTranscriptCommitted;
            }
        }

        private void OnDisable()
        {
            if (_toggleButton != null)
            {
                _toggleButton.onClick.RemoveListener(OnToggleClicked);
            }

            if (_driver != null)
            {
                _driver.ListeningStateChanged -= OnListeningStateChanged;
            }

            if (_commandCapture != null)
            {
                _commandCapture.TranscriptCommitted -= OnTranscriptCommitted;
            }
        }

        /// <summary>
        /// Mục tiêu: Một nút — đang dừng thì bắt đầu thu, đang thu thì dừng.
        /// Cách hoạt động: Đọc IsListening trên driver → StartListeningAsync hoặc StopListening.
        /// </summary>
        /// <summary>Gắn vào Button → On Click () hoặc gọi từ script.</summary>
        public void OnToggleClicked()
        {
            if (_commandCapture != null)
            {
                if (_commandCapture.IsCapturing)
                {
                    return;
                }

                SetStatus("Đang nghe… (V)");
                GameEventLog.Post("[Voice] Bắt đầu thu (V / nút).", GameEventLogCategory.Info);
                _commandCapture.BeginCapture();
                RefreshToggleUi(true);
                return;
            }

            if (_driver == null)
            {
                SetStatus("Thiếu WhisperSttOnlyDriver trong scene.");
                return;
            }

            _driver.ToggleListening();
        }

        private void OnTranscriptCommitted(string transcript)
        {
            RefreshToggleUi(false);
            SetStatus("Nhấn V hoặc nút để nói tiếp.");
        }

        private void OnListeningStateChanged(bool listening)
        {
            RefreshToggleUi(listening);
        }

        private void RefreshToggleUi(bool listening)
        {
            if (_toggleButtonLabel != null)
            {
                _toggleButtonLabel.text = _commandCapture != null
                    ? (listening ? "Đang nghe…" : "Nói (V)")
                    : (listening ? "Dừng thu" : "Bắt đầu thu");
            }

            if (_toggleButtonImage != null)
            {
                _toggleButtonImage.color = listening ? RecordingButtonColor : IdleButtonColor;
            }

            if (listening)
            {
                SetStatus(_commandCapture != null ? "Đang nghe… (V)" : "Đang thu âm…");
            }
        }

        private void CacheToggleGraphic()
        {
            if (_toggleButton == null)
            {
                return;
            }

            _toggleButtonImage = _toggleButton.targetGraphic as Image;
            if (_toggleButtonImage == null)
            {
                _toggleButtonImage = _toggleButton.GetComponent<Image>();
            }

            if (_toggleButtonLabel == null)
            {
                _toggleButtonLabel = _toggleButton.GetComponentInChildren<Text>();
            }
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }
        }

        /// <summary>
        /// Mục tiêu: Tạo Canvas tối thiểu khi prefab chưa có UI gán sẵn.
        /// Cách hoạt động: EventSystem + overlay + transcript + một nút toggle + gán reference.
        /// </summary>
        private void BuildRuntimeUi()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("SttRecordUI");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            var panel = CreateUiObject("Panel", canvasGo.transform);
            StretchFull(panel);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);

            var statusGo = CreateUiObject("Status", panel);
            var statusRect = statusGo;
            statusRect.anchorMin = new Vector2(0.05f, 0.88f);
            statusRect.anchorMax = new Vector2(0.95f, 0.98f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            _statusText = statusGo.gameObject.AddComponent<Text>();
            ConfigureLabel(_statusText, font, 22, new Color(0.75f, 0.85f, 1f));
            _statusText.text = _commandCapture != null
                ? "Nhấn V hoặc nút để nói."
                : "Đã dừng — nhấn nút để bắt đầu thu.";

            var buttonRow = CreateUiObject("ToggleButton", panel);
            var rowRect = buttonRow;
            rowRect.anchorMin = new Vector2(0.25f, 0.02f);
            rowRect.anchorMax = new Vector2(0.75f, 0.12f);
            rowRect.offsetMin = Vector2.zero;
            rowRect.offsetMax = Vector2.zero;

            var buttonLabel = _commandCapture != null ? "Nói (V)" : "Bắt đầu thu";
            _toggleButton = CreateButton(buttonRow, font, buttonLabel, IdleButtonColor);
            _toggleButtonLabel = _toggleButton.GetComponentInChildren<Text>();

            var transcriptGo = CreateUiObject("Transcript", panel);
            var transcriptRect = transcriptGo;
            transcriptRect.anchorMin = new Vector2(0.05f, 0.14f);
            transcriptRect.anchorMax = new Vector2(0.95f, 0.86f);
            transcriptRect.offsetMin = Vector2.zero;
            transcriptRect.offsetMax = Vector2.zero;
            var transcript = transcriptGo.gameObject.AddComponent<Text>();
            ConfigureLabel(transcript, font, 30, Color.white);
            transcript.alignment = TextAnchor.UpperLeft;
            transcript.text = "STT — chờ bắt đầu thu.";

            _driver?.AssignTranscriptText(transcript);
        }

        private static RectTransform CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ConfigureLabel(Text text, Font font, int size, Color color)
        {
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static Button CreateButton(RectTransform parent, Font font, string label, Color bg)
        {
            StretchFull(parent);

            var image = parent.gameObject.AddComponent<Image>();
            image.color = bg;

            var button = parent.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var textRect = CreateUiObject("Label", parent);
            StretchFull(textRect);
            var text = textRect.gameObject.AddComponent<Text>();
            ConfigureLabel(text, font, 26, Color.white);
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;

            return button;
        }
    }
}

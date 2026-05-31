using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.Audio
{
    /// <summary>
    /// SRP: Âm thanh menu — nhạc nền MenuMusic + click UiSelect trên mọi Button con.
    /// MainMenu: startMenuMusic=true. SSScene: startMenuMusic=false (giữ nhạc từ MainMenu qua AudioBootstrap DontDestroyOnLoad).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuAudioController : MonoBehaviour
    {
        [SerializeField] bool startMenuMusic = true;
        [SerializeField] AudioCueId buttonClickCue = AudioCueId.UiSelect;
        [SerializeField] bool includeInactiveButtons = true;
        [SerializeField] Transform buttonSearchRoot;

        readonly System.Collections.Generic.List<Button> boundButtons = new(32);
        readonly System.Collections.Generic.List<UnityEngine.Events.UnityAction> boundActions = new(32);

        void Awake()
        {
            AudioBootstrap.EnsureExists();
            AudioVolumeController volume = FindFirstObjectByType<AudioVolumeController>(FindObjectsInactive.Include);
            volume?.ApplyAllFromStorage();
        }

        void Start()
        {
            BindAllButtons();

            if (startMenuMusic)
            {
                AudioAccess.TryStartMenuMusic();
            }
        }

        void OnDestroy()
        {
            UnbindAllButtons();
        }

        /// <summary>
        /// Mục tiêu: Gắn lại sau khi thêm button UI runtime (dialog, v.v.).
        /// Cách hoạt động: Unbind cũ rồi quét lại Button dưới buttonSearchRoot.
        /// </summary>
        public void RefreshButtonBindings()
        {
            BindAllButtons();
        }

        void BindAllButtons()
        {
            UnbindAllButtons();

            Transform root = buttonSearchRoot != null ? buttonSearchRoot : transform;
            Button[] buttons = root.GetComponentsInChildren<Button>(includeInactiveButtons);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                UnityEngine.Events.UnityAction action = PlayButtonClick;
                button.onClick.AddListener(action);
                boundButtons.Add(button);
                boundActions.Add(action);
            }
        }

        void UnbindAllButtons()
        {
            for (int i = 0; i < boundButtons.Count; i++)
            {
                if (boundButtons[i] != null && boundActions[i] != null)
                {
                    boundButtons[i].onClick.RemoveListener(boundActions[i]);
                }
            }

            boundButtons.Clear();
            boundActions.Clear();
        }

        /// <summary>
        /// Mục tiêu: Phát tiếng click khi bấm nút menu.
        /// Cách hoạt động: Gọi AudioAccess.TryPlay với cue UiSelect (0 A.D. ui_select.ogg).
        /// </summary>
        void PlayButtonClick()
        {
            AudioAccess.TryPlay(buttonClickCue);
        }
    }
}

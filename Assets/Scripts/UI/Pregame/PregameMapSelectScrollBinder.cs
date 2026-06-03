using System;
using System.Collections.Generic;
using GameDevTV.RTS.Game.Pregame;
using GameDevTV.RTS.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.UI.Pregame
{
    /// <summary>
    /// SRP: Nguồn cấu hình map duy nhất trên SSScene — catalog, spawn nút, preview, scene đã chọn.
    /// Chỉ chỉnh mảng Maps + template/Content trên component này (không khai báo lại trên PregameSetupUIController).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PregameMapSelectScrollBinder : MonoBehaviour
    {
        [Header("Catalog")]
        [SerializeField] PregameMapEntry[] maps =
        {
            new() { displayName = "Game 1", gameplaySceneName = "Game 1" },
            new() { displayName = "Game 2", gameplaySceneName = "Game 2" }
        };

        [Header("Scroll list")]
        [SerializeField] Transform mapListContent;
        [SerializeField] GameObject mapButtonTemplate;

        [Header("Preview")]
        [SerializeField] Image mapPreviewImage;
        [SerializeField] TMP_Text mapNameLabel;

        [Header("Optional auto-bind")]
        [SerializeField] Transform searchRoot;

        UiExclusiveSelectGroup _mapSelectGroup;
        PregameMapEntry[] _activeMaps = Array.Empty<PregameMapEntry>();
        bool _interactionEnabled = true;
        bool _suppressSelectionBroadcast;

        public event Action<int> SelectionChanged;

        public int SelectedIndex { get; private set; }
        public int MapCount => _activeMaps.Length;

        /// <summary>
        /// Mục tiêu: Bỏ qua gói SyncVar tạm (index/scene lệch frame) trước khi áp UI client.
        /// Cách hoạt động: So sánh scene tại index trong catalog với scene host gửi (đã normalize).
        /// </summary>
        public bool IsIndexScenePairConsistent(int index, string gameplaySceneName)
        {
            if (_activeMaps.Length == 0 || string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                return false;
            }

            index = Mathf.Clamp(index, 0, _activeMaps.Length - 1);
            string catalogScene = ProjectRTS.Netplay.RtsNetSceneUtility.NormalizeSceneName(
                _activeMaps[index].gameplaySceneName);
            string hostScene = ProjectRTS.Netplay.RtsNetSceneUtility.NormalizeSceneName(gameplaySceneName);
            return catalogScene == hostScene;
        }

        /// <summary>
        /// Mục tiêu: Cho controller đọc scene gameplay của map đang chọn khi bấm Start.
        /// Cách hoạt động: Clamp index vào mảng map đã lọc; fallback DefaultGameplayScene nếu rỗng.
        /// </summary>
        public string GetSelectedGameplayScene()
        {
            if (_activeMaps.Length == 0)
            {
                return PregameSessionState.DefaultGameplayScene;
            }

            int index = Mathf.Clamp(SelectedIndex, 0, _activeMaps.Length - 1);
            string scene = _activeMaps[index].gameplaySceneName;
            return string.IsNullOrWhiteSpace(scene)
                ? PregameSessionState.DefaultGameplayScene
                : scene;
        }

        /// <summary>
        /// Mục tiêu: Khởi tạo danh sách map động khi vào SSScene.
        /// Cách hoạt động: Lọc map hợp lệ, spawn button từ template, refresh exclusive group.
        /// </summary>
        public void BuildMapList()
        {
            ResolveReferences();
            _activeMaps = SanitizeMaps(maps);
            SpawnMapButtons();
            RefreshSelectGroup();
            WireSpawnedMapButtons();
            ApplyInteractionStateToItems();
            ApplySelectedMapPresentation(0);
            SelectedIndex = 0;
        }

        /// <summary>
        /// Mục tiêu: Chọn map mặc định (map đầu tiên) và cập nhật UI preview.
        /// Cách hoạt động: Gọi Select trên group với notify=true để kích hoạt SelectionChanged.
        /// </summary>
        public void SelectDefaultMap()
        {
            if (_mapSelectGroup == null || _activeMaps.Length == 0)
            {
                return;
            }

            _mapSelectGroup.Select(0, notify: true);
        }

        public void SubscribeSelectionChanged()
        {
            if (_mapSelectGroup != null)
            {
                _mapSelectGroup.SelectionChanged += HandleMapSelectionChanged;
            }
        }

        public void UnsubscribeSelectionChanged()
        {
            if (_mapSelectGroup != null)
            {
                _mapSelectGroup.SelectionChanged -= HandleMapSelectionChanged;
            }
        }

        /// <summary>
        /// Mục tiêu: Khóa/mở chọn map (client trong phòng chỉ xem map host chọn).
        /// Cách hoạt động: Tắt interactable và đổi màu nút map theo trạng thái enabled.
        /// </summary>
        public void SetInteractionEnabled(bool enabled)
        {
            _interactionEnabled = enabled;
            ApplyInteractionStateToItems();
        }

        /// <summary>
        /// Mục tiêu: Client áp map host chọn qua mạng mà không gửi lại server.
        /// Cách hoạt động: Select exclusive group với notify=false và refresh preview.
        /// </summary>
        public void ApplyNetworkSelection(int index)
        {
            if (_mapSelectGroup == null || _activeMaps.Length == 0)
            {
                return;
            }

            index = Mathf.Clamp(index, 0, _activeMaps.Length - 1);
            _suppressSelectionBroadcast = true;
            _mapSelectGroup.Select(index, notify: false);
            SelectedIndex = index;
            ApplySelectedMapPresentation(index);
            _suppressSelectionBroadcast = false;
        }

        /// <summary>
        /// Mục tiêu: Fallback khi index khác giữa hai máy nhưng scene name trùng catalog.
        /// Cách hoạt động: Tìm index theo gameplaySceneName rồi gọi ApplyNetworkSelection.
        /// </summary>
        public void ApplyNetworkSelectionByScene(string gameplaySceneName)
        {
            if (_activeMaps.Length == 0 || string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                return;
            }

            string normalized = ProjectRTS.Netplay.RtsNetSceneUtility.NormalizeSceneName(gameplaySceneName);
            for (int i = 0; i < _activeMaps.Length; i++)
            {
                string catalogScene = ProjectRTS.Netplay.RtsNetSceneUtility.NormalizeSceneName(_activeMaps[i].gameplaySceneName);
                if (catalogScene == normalized)
                {
                    ApplyNetworkSelection(i);
                    return;
                }
            }
        }

        void ApplyInteractionStateToItems()
        {
            if (mapListContent == null)
            {
                return;
            }

            PregameMapSelectItemView[] itemViews =
                mapListContent.GetComponentsInChildren<PregameMapSelectItemView>(false);

            for (int i = 0; i < itemViews.Length; i++)
            {
                itemViews[i].SetInteractionEnabled(_interactionEnabled);
            }
        }

        void HandleMapSelectionChanged(int index)
        {
            SelectedIndex = index;
            ApplySelectedMapPresentation(index);
            if (!_suppressSelectionBroadcast)
            {
                SelectionChanged?.Invoke(SelectedIndex);
            }
        }

        void ApplySelectedMapPresentation(int index)
        {
            if (_activeMaps.Length == 0)
            {
                return;
            }

            index = Mathf.Clamp(index, 0, _activeMaps.Length - 1);
            PregameMapEntry entry = _activeMaps[index];

            if (mapNameLabel != null)
            {
                mapNameLabel.text = entry.displayName;
            }

            if (mapPreviewImage == null)
            {
                return;
            }

            mapPreviewImage.preserveAspect = true;
            mapPreviewImage.sprite = entry.previewSprite;
            mapPreviewImage.enabled = entry.previewSprite != null;
        }

        void ResolveReferences()
        {
            Transform root = searchRoot != null ? searchRoot : transform;

            if (mapListContent == null)
            {
                mapListContent = FindMapListContent(root);
            }

            if (mapPreviewImage == null)
            {
                Transform preview = FindDeepChild(root, "Image_Map");
                if (preview != null)
                {
                    mapPreviewImage = preview.GetComponent<Image>();
                }
            }

            if (mapNameLabel == null)
            {
                Transform label = FindDeepChild(root, "Map name");
                if (label != null)
                {
                    mapNameLabel = label.GetComponent<TMP_Text>();
                }
            }

            if (mapButtonTemplate == null && mapListContent != null)
            {
                mapButtonTemplate = FindFirstMapButtonTemplate(mapListContent);
            }
        }

        /// <summary>
        /// Mục tiêu: Tạo đúng số nút map theo catalog (không phụ thuộc Map 1–4 cắm sẵn trong scene).
        /// Cách hoạt động: Tách template khỏi Content, xóa con cũ, Instantiate từng entry và Bind view.
        /// </summary>
        void SpawnMapButtons()
        {
            if (mapListContent == null)
            {
                Debug.LogError("[PregameMapSelect] Thiếu Content scrollview map.", this);
                return;
            }

            if (mapButtonTemplate == null)
            {
                Debug.LogError("[PregameMapSelect] Thiếu Map Button Template (kéo 1 nút map mẫu vào Inspector).", this);
                return;
            }

            if (_activeMaps.Length == 0)
            {
                Debug.LogError("[PregameMapSelect] Không có map hợp lệ. Kiểm tra Build Settings.", this);
                return;
            }

            DetachTemplateFromContent();
            ClearMapListContent();

            for (int i = 0; i < _activeMaps.Length; i++)
            {
                GameObject instance = Instantiate(mapButtonTemplate, mapListContent);
                instance.SetActive(true);

                PregameMapSelectItemView itemView = instance.GetComponent<PregameMapSelectItemView>();
                if (itemView == null)
                {
                    itemView = instance.AddComponent<PregameMapSelectItemView>();
                }

                itemView.Bind(_activeMaps[i], null, i);
            }
        }

        /// <summary>
        /// Mục tiêu: Sau RefreshOptions, gán lại group/index cho từng nút map vừa spawn.
        /// Cách hoạt động: Quét PregameMapSelectItemView trên Content và gọi Bind lại với index đúng.
        /// </summary>
        void WireSpawnedMapButtons()
        {
            if (mapListContent == null || _mapSelectGroup == null)
            {
                return;
            }

            PregameMapSelectItemView[] itemViews =
                mapListContent.GetComponentsInChildren<PregameMapSelectItemView>(false);

            for (int i = 0; i < itemViews.Length && i < _activeMaps.Length; i++)
            {
                itemViews[i].Bind(_activeMaps[i], _mapSelectGroup, i);
            }
        }

        void DetachTemplateFromContent()
        {
            if (mapButtonTemplate.transform.parent == mapListContent)
            {
                mapButtonTemplate.transform.SetParent(mapListContent.parent, false);
            }

            mapButtonTemplate.SetActive(false);
        }

        void ClearMapListContent()
        {
            for (int i = mapListContent.childCount - 1; i >= 0; i--)
            {
                Transform child = mapListContent.GetChild(i);
                if (child.gameObject == mapButtonTemplate)
                {
                    continue;
                }

                Destroy(child.gameObject);
            }
        }

        void RefreshSelectGroup()
        {
            if (mapListContent == null)
            {
                return;
            }

            _mapSelectGroup = mapListContent.GetComponent<UiExclusiveSelectGroup>();
            if (_mapSelectGroup == null)
            {
                _mapSelectGroup = mapListContent.gameObject.AddComponent<UiExclusiveSelectGroup>();
            }

            _mapSelectGroup.RefreshOptions();
        }

        static PregameMapEntry[] SanitizeMaps(PregameMapEntry[] source)
        {
            if (source == null || source.Length == 0)
            {
                return CreateDefaultMaps();
            }

            var sanitized = new List<PregameMapEntry>(source.Length);
            var usedScenes = new HashSet<string>();

            for (int i = 0; i < source.Length; i++)
            {
                PregameMapEntry entry = source[i];
                string sceneName = entry.gameplaySceneName?.Trim();
                if (string.IsNullOrWhiteSpace(sceneName) || !IsSceneLoadable(sceneName))
                {
                    continue;
                }

                if (!usedScenes.Add(sceneName))
                {
                    continue;
                }

                sanitized.Add(new PregameMapEntry
                {
                    displayName = string.IsNullOrWhiteSpace(entry.displayName) ? sceneName : entry.displayName.Trim(),
                    gameplaySceneName = sceneName,
                    previewSprite = entry.previewSprite
                });
            }

            return sanitized.Count > 0 ? sanitized.ToArray() : CreateDefaultMaps();
        }

        static PregameMapEntry[] CreateDefaultMaps()
        {
            return new[]
            {
                new PregameMapEntry { displayName = "Game 1", gameplaySceneName = "Game 1" },
                new PregameMapEntry { displayName = "Game 2", gameplaySceneName = "Game 2" }
            };
        }

        static bool IsSceneLoadable(string sceneName) =>
            !string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);

        static GameObject FindFirstMapButtonTemplate(Transform content)
        {
            Button[] buttons = content.GetComponentsInChildren<Button>(true);
            return buttons.Length > 0 ? buttons[0].gameObject : null;
        }

        static Transform FindMapListContent(Transform root)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name != "Map Panel")
                {
                    continue;
                }

                Transform content = FindDeepChild(all[i], "Content");
                if (content != null)
                {
                    return content;
                }
            }

            return null;
        }

        static Transform FindDeepChild(Transform parent, string name)
        {
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

using System.Collections.Generic;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Minimap
{
    /// <summary>
    /// Icon supply trên minimap: hiện khi đang trong vision; sau lần đầu mất vision thì luôn hiện.
    /// </summary>
    [DisallowMultipleComponent]
    public class MinimapSupplyIconsController : MonoBehaviour
    {
        [SerializeField] private MinimapMapBoundsSO mapBounds;
        [SerializeField] private MinimapIconStyleSO iconStyle;
        [SerializeField] private MinimapIconView iconPrefab;
        [SerializeField] private RectTransform iconsRoot;

        private readonly Dictionary<GatherableSupply, MinimapIconView> activeIcons = new(256);
        private readonly Queue<MinimapIconView> iconPool = new(128);
        private readonly HashSet<GatherableSupply> pinnedOnMinimap = new(256);
        private readonly Dictionary<GatherableSupply, bool> wasVisibleOnce = new(256);

        private void Awake()
        {
            if (iconsRoot == null)
            {
                iconsRoot = transform as RectTransform;
            }
        }

        private void OnEnable()
        {
            Bus<SupplySpawnEvent>.RegisterForAll(HandleSupplySpawn);
            Bus<SupplyDepletedEvent>.RegisterForAll(HandleSupplyDepleted);
            RegisterExistingSupplies();
        }

        private void OnDisable()
        {
            Bus<SupplySpawnEvent>.UnregisterForAll(HandleSupplySpawn);
            Bus<SupplyDepletedEvent>.UnregisterForAll(HandleSupplyDepleted);
            ClearAllIcons();
        }

        private void LateUpdate()
        {
            if (mapBounds == null)
            {
                return;
            }

            foreach (KeyValuePair<GatherableSupply, MinimapIconView> pair in activeIcons)
            {
                GatherableSupply supply = pair.Key;
                MinimapIconView view = pair.Value;
                if (supply == null || view == null)
                {
                    continue;
                }

                Transform tracked = supply.Transform;
                if (tracked == null)
                {
                    continue;
                }

                view.SetNormalizedPosition(mapBounds.WorldToNormalized(tracked.position));
                UpdateIconVisibility(supply, view);
            }
        }

        public void Configure(
            MinimapMapBoundsSO bounds,
            MinimapIconStyleSO style,
            MinimapIconView prefab,
            MinimapFogSystemReference fog = null)
        {
            mapBounds = bounds;
            iconStyle = style;
            if (prefab != null)
            {
                iconPrefab = prefab;
            }
        }

        private void RegisterExistingSupplies()
        {
            GatherableSupply[] supplies = FindObjectsByType<GatherableSupply>(FindObjectsSortMode.None);
            for (int i = 0; i < supplies.Length; i++)
            {
                TryAddSupply(supplies[i]);
            }
        }

        private void HandleSupplySpawn(SupplySpawnEvent evt) => TryAddSupply(evt.Supply);

        private void HandleSupplyDepleted(SupplyDepletedEvent evt) => RemoveSupply(evt.Supply);

        private void TryAddSupply(GatherableSupply supply)
        {
            if (supply == null || iconStyle == null || iconPrefab == null || iconsRoot == null)
            {
                return;
            }

            if (activeIcons.ContainsKey(supply))
            {
                return;
            }

            MinimapIconView view = RentIconView();
            MinimapMarkerPresentation presentation = iconStyle.BuildForSupply(supply);
            view.Configure(presentation);
            view.SetNormalizedPosition(mapBounds != null
                ? mapBounds.WorldToNormalized(supply.transform.position)
                : new Vector2(0.5f, 0.5f));

            if (supply.IsVisible)
            {
                wasVisibleOnce[supply] = true;
            }

            supply.OnVisibilityChanged += HandleVisibilityChanged;
            UpdateIconVisibility(supply, view);
            activeIcons.Add(supply, view);
        }

        private void RemoveSupply(GatherableSupply supply)
        {
            if (supply == null || !activeIcons.TryGetValue(supply, out MinimapIconView view))
            {
                return;
            }

            supply.OnVisibilityChanged -= HandleVisibilityChanged;
            pinnedOnMinimap.Remove(supply);
            wasVisibleOnce.Remove(supply);
            activeIcons.Remove(supply);
            ReturnIconView(view);
        }

        private void HandleVisibilityChanged(IHideable hideable, bool isVisible)
        {
            if (hideable is not GatherableSupply supply)
            {
                return;
            }

            if (isVisible)
            {
                wasVisibleOnce[supply] = true;
            }
            else if (wasVisibleOnce.TryGetValue(supply, out bool seen) && seen)
            {
                pinnedOnMinimap.Add(supply);
            }

            if (activeIcons.TryGetValue(supply, out MinimapIconView view) && view != null)
            {
                UpdateIconVisibility(supply, view);
            }
        }

        /// <summary>
        /// Mục tiêu: Supply hiện khi đang nhìn thấy; sau lần đầu IsVisible = false (đã từng thấy) thì ghim trên minimap.
        /// Cách hoạt động: Không dùng fog explored RT; chỉ vision + bộ nhớ pinnedOnMinimap.
        /// </summary>
        private void UpdateIconVisibility(GatherableSupply supply, MinimapIconView view)
        {
            bool visible = supply.IsVisible || pinnedOnMinimap.Contains(supply);
            view.SetVisible(visible);
        }

        private MinimapIconView RentIconView()
        {
            while (iconPool.Count > 0)
            {
                MinimapIconView pooled = iconPool.Dequeue();
                if (pooled != null)
                {
                    pooled.gameObject.SetActive(true);
                    return pooled;
                }
            }

            return Instantiate(iconPrefab, iconsRoot);
        }

        private void ReturnIconView(MinimapIconView view)
        {
            if (view == null)
            {
                return;
            }

            view.gameObject.SetActive(false);
            iconPool.Enqueue(view);
        }

        private void ClearAllIcons()
        {
            foreach (KeyValuePair<GatherableSupply, MinimapIconView> pair in activeIcons)
            {
                if (pair.Key != null)
                {
                    pair.Key.OnVisibilityChanged -= HandleVisibilityChanged;
                }

                ReturnIconView(pair.Value);
            }

            activeIcons.Clear();
            pinnedOnMinimap.Clear();
            wasVisibleOnce.Clear();
        }
    }
}

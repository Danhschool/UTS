using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Minimap
{
    /// <summary>
    /// Hiển thị icon unit/building trên minimap UI (overlay), dùng UnitSO.Icon — không gắn object 3D trên đầu unit.
    /// </summary>
    [DisallowMultipleComponent]
    public class MinimapUnitIconsController : MonoBehaviour
    {
        [SerializeField] private MinimapMapBoundsSO mapBounds;
        [SerializeField] private MinimapIconStyleSO iconStyle;
        [SerializeField] private MinimapIconView iconPrefab;
        [SerializeField] private RectTransform iconsRoot;
        [SerializeField] private bool trackPlayerUnitsOnly;
        [SerializeField] private MinimapFogSystemReference fogSystem;
        [SerializeField] private bool hideIconsOutsideExplored = true;

        private readonly Dictionary<AbstractCommandable, MinimapIconView> activeIcons = new(128);
        private readonly Queue<MinimapIconView> iconPool = new(64);

        private void Awake()
        {
            if (iconsRoot == null)
            {
                iconsRoot = transform as RectTransform;
            }
        }

        private void OnEnable()
        {
            Bus<UnitSpawnEvent>.RegisterForAll(HandleUnitSpawn);
            Bus<UnitDeathEvent>.RegisterForAll(HandleUnitDeath);
            Bus<BuildingSpawnEvent>.RegisterForAll(HandleBuildingSpawn);
            Bus<BuildingDeathEvent>.RegisterForAll(HandleBuildingDeath);

            RegisterExistingCommandables();
        }

        private void OnDisable()
        {
            Bus<UnitSpawnEvent>.UnregisterForAll(HandleUnitSpawn);
            Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);
            Bus<BuildingSpawnEvent>.UnregisterForAll(HandleBuildingSpawn);
            Bus<BuildingDeathEvent>.UnregisterForAll(HandleBuildingDeath);

            ClearAllIcons();
        }

        private void LateUpdate()
        {
            if (mapBounds == null)
            {
                return;
            }

            foreach (KeyValuePair<AbstractCommandable, MinimapIconView> pair in activeIcons)
            {
                AbstractCommandable commandable = pair.Key;
                MinimapIconView view = pair.Value;
                if (commandable == null || view == null)
                {
                    continue;
                }

                Transform tracked = commandable.Transform;
                if (tracked == null)
                {
                    continue;
                }

                view.SetNormalizedPosition(mapBounds.WorldToNormalized(tracked.position));
                UpdateIconVisibility(commandable, view);
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
            fogSystem = fog;
            if (prefab != null)
            {
                iconPrefab = prefab;
            }

            fogSystem?.EnsureReferences();
        }

        private void RegisterExistingCommandables()
        {
            AbstractUnit[] units = FindObjectsByType<AbstractUnit>(FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                TryAddCommandable(units[i]);
            }

            BaseBuilding[] buildings = FindObjectsByType<BaseBuilding>(FindObjectsSortMode.None);
            for (int i = 0; i < buildings.Length; i++)
            {
                TryAddCommandable(buildings[i]);
            }
        }

        private void HandleUnitSpawn(UnitSpawnEvent evt) => TryAddCommandable(evt.Unit);

        private void HandleUnitDeath(UnitDeathEvent evt) => RemoveCommandable(evt.Unit);

        private void HandleBuildingSpawn(BuildingSpawnEvent evt) => TryAddCommandable(evt.Building);

        private void HandleBuildingDeath(BuildingDeathEvent evt) => RemoveCommandable(evt.Building);

        private void TryAddCommandable(AbstractCommandable commandable)
        {
            if (commandable == null || iconStyle == null || iconPrefab == null || iconsRoot == null)
            {
                return;
            }

            if (trackPlayerUnitsOnly && commandable.Owner != Owner.Player1)
            {
                return;
            }

            if (activeIcons.ContainsKey(commandable))
            {
                return;
            }

            MinimapIconView view = RentIconView();
            MinimapMarkerPresentation presentation = iconStyle.BuildFor(commandable);
            view.Configure(presentation);
            view.SetNormalizedPosition(mapBounds != null
                ? mapBounds.WorldToNormalized(commandable.transform.position)
                : new Vector2(0.5f, 0.5f));
            UpdateIconVisibility(commandable, view);

            commandable.OnVisibilityChanged += HandleVisibilityChanged;
            activeIcons.Add(commandable, view);
        }

        private void RemoveCommandable(AbstractCommandable commandable)
        {
            if (commandable == null || !activeIcons.TryGetValue(commandable, out MinimapIconView view))
            {
                return;
            }

            commandable.OnVisibilityChanged -= HandleVisibilityChanged;
            activeIcons.Remove(commandable);
            ReturnIconView(view);
        }

        private void HandleVisibilityChanged(IHideable hideable, bool isVisible)
        {
            if (hideable is not AbstractCommandable commandable)
            {
                return;
            }

            if (activeIcons.TryGetValue(commandable, out MinimapIconView view) && view != null)
            {
                UpdateIconVisibility(commandable, view);
            }
        }

        private void UpdateIconVisibility(AbstractCommandable commandable, MinimapIconView view)
        {
            if (commandable.Owner == Owner.Player1)
            {
                view.SetVisible(true);
                return;
            }

            bool visible = commandable.IsVisible;
            if (hideIconsOutsideExplored && fogSystem != null && commandable.Transform != null)
            {
                visible &= fogSystem.IsWorldPositionExplored(commandable.Transform.position);
            }

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
            foreach (KeyValuePair<AbstractCommandable, MinimapIconView> pair in activeIcons)
            {
                if (pair.Key != null)
                {
                    pair.Key.OnVisibilityChanged -= HandleVisibilityChanged;
                }

                ReturnIconView(pair.Value);
            }

            activeIcons.Clear();
        }
    }
}

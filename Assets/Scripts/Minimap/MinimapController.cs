using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.UI;

namespace GameDevTV.RTS.Minimap
{
    public class MinimapController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MinimapMapBoundsSO mapBounds;
        [SerializeField] private MinimapIconStyleSO iconStyle;

        [Header("UI layers")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private RectTransform iconsRoot;
        [SerializeField] private MinimapFogOverlay fogOverlay;
        [SerializeField] private MinimapCameraViewport cameraViewport;
        [SerializeField] private MinimapInputHandler inputHandler;

        [Header("Prefab")]
        [SerializeField] private MinimapIconView iconPrefab;

        [Header("Fog")]
        [SerializeField] private RenderTexture exploredFogTexture;

        [Header("Camera")]
        [SerializeField] private MonoBehaviour cameraNavigatorBehaviour;

        private readonly Dictionary<AbstractCommandable, MinimapIconView> icons = new(128);

        private void Awake()
        {
            ResolveReferences();
            WireCameraNavigator();
            if (fogOverlay != null && exploredFogTexture != null)
            {
                fogOverlay.SetExploredTexture(exploredFogTexture);
            }
        }

        private void WireCameraNavigator()
        {
            if (cameraNavigatorBehaviour is not IMinimapCameraNavigator)
            {
                cameraNavigatorBehaviour = FindFirstObjectByType<PlayerInput>();
            }

            if (cameraNavigatorBehaviour == null)
            {
                return;
            }

            inputHandler?.SetCameraNavigator(cameraNavigatorBehaviour);
            cameraViewport?.SetCameraNavigator(cameraNavigatorBehaviour);
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
            ClearIcons();
        }

        private void LateUpdate()
        {
            if (mapBounds == null)
            {
                return;
            }

            foreach (KeyValuePair<AbstractCommandable, MinimapIconView> pair in icons)
            {
                AbstractCommandable commandable = pair.Key;
                MinimapIconView view = pair.Value;
                if (commandable == null || view == null)
                {
                    continue;
                }

                view.SetNormalizedPosition(mapBounds.WorldToNormalized(commandable.transform.position));
                view.SetVisible(ShouldShowOnMinimap(commandable));
            }
        }

        private void ResolveReferences()
        {
            Transform background = transform.Find("Background");
            if (backgroundImage == null && background != null)
            {
                backgroundImage = background.GetComponent<Image>();
            }

            Transform mask = background != null ? background.Find("Minimap Mask") : null;
            if (iconsRoot == null && mask != null)
            {
                Transform icons = mask.Find("Icons");
                if (icons == null)
                {
                    icons = mask.Find("Minimap");
                }

                iconsRoot = icons as RectTransform;
            }

            if (fogOverlay == null && mask != null)
            {
                Transform fog = mask.Find("Fog Overlay");
                if (fog != null)
                {
                    fogOverlay = fog.GetComponent<MinimapFogOverlay>();
                }
            }

            if (cameraViewport == null && mask != null)
            {
                Transform viewport = mask.Find("Camera Viewport");
                if (viewport != null)
                {
                    cameraViewport = viewport.GetComponent<MinimapCameraViewport>();
                }
            }

            if (inputHandler == null && mask != null)
            {
                inputHandler = mask.GetComponent<MinimapInputHandler>();
            }
        }

        private void RegisterExistingCommandables()
        {
            AbstractCommandable[] commandables = FindObjectsByType<AbstractCommandable>(FindObjectsSortMode.None);
            foreach (AbstractCommandable commandable in commandables)
            {
                TryRegister(commandable);
            }
        }

        private void HandleUnitSpawn(UnitSpawnEvent evt) => TryRegister(evt.Unit);
        private void HandleUnitDeath(UnitDeathEvent evt) => TryUnregister(evt.Unit);
        private void HandleBuildingSpawn(BuildingSpawnEvent evt) => TryRegister(evt.Building);
        private void HandleBuildingDeath(BuildingDeathEvent evt) => TryUnregister(evt.Building);

        private void TryRegister(AbstractCommandable commandable)
        {
            if (commandable == null || commandable.UnitSO == null || icons.ContainsKey(commandable))
            {
                return;
            }

            if (iconPrefab == null || iconsRoot == null || iconStyle == null)
            {
                return;
            }

            MinimapIconView view = Instantiate(iconPrefab, iconsRoot);
            view.Configure(
                iconStyle.IconSprite != null ? iconStyle.IconSprite : commandable.UnitSO.Icon,
                iconStyle.GetColor(commandable.Owner),
                iconStyle.IconSize);
            view.SetNormalizedPosition(mapBounds.WorldToNormalized(commandable.transform.position));
            view.SetVisible(ShouldShowOnMinimap(commandable));
            icons.Add(commandable, view);
        }

        private void TryUnregister(AbstractCommandable commandable)
        {
            if (commandable == null || !icons.TryGetValue(commandable, out MinimapIconView view))
            {
                return;
            }

            icons.Remove(commandable);
            if (view != null)
            {
                Destroy(view.gameObject);
            }
        }

        private void ClearIcons()
        {
            foreach (MinimapIconView view in icons.Values)
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            icons.Clear();
        }

        private static bool ShouldShowOnMinimap(AbstractCommandable commandable)
        {
            if (commandable.Owner == Owner.Player1)
            {
                return true;
            }

            return commandable.IsVisible;
        }
    }
}

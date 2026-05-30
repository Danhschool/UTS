using GameDevTV.RTS.Hotkeys.Handlers;
using GameDevTV.RTS.Hotkeys.Targets;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// SRP: Gắn prefab nhà → HotkeyId A/S/D và đăng ký delegate lên HotkeyService.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingTypeHotkeySetup : MonoBehaviour
    {
        [Header("Prefab archetype (BuildingSO.Prefab)")]
        [SerializeField] GameObject civilCentralPrefab;
        [SerializeField] GameObject forgePrefab;
        [SerializeField] GameObject barrackPrefab;

        void Start()
        {
#if UNITY_EDITOR
            TryAssignDefaultPrefabsInEditor();
#endif
            HotkeySystem hotkeySystem = GetComponent<HotkeySystem>();
            IHotkeyUnitTypeSelectTarget target = GetComponent<PlayerInputHotkeyIntegration>();
            if (target == null)
            {
                target = GetComponent<PlayerInput>();
            }

            if (hotkeySystem?.Service == null || target == null)
            {
                Debug.LogWarning(
                    $"[{nameof(BuildingTypeHotkeySetup)}] Thiếu HotkeySystem hoặc IHotkeyUnitTypeSelectTarget trên {name}.",
                    this);
                return;
            }

            RegisterIfAssigned(hotkeySystem.Service, target, HotkeyId.SelectCivilCentralOnScreen, civilCentralPrefab);
            RegisterIfAssigned(hotkeySystem.Service, target, HotkeyId.SelectForgeOnScreen, forgePrefab);
            RegisterIfAssigned(hotkeySystem.Service, target, HotkeyId.SelectBarracksOnScreen, barrackPrefab);
        }

        static void RegisterIfAssigned(
            HotkeyService service,
            IHotkeyUnitTypeSelectTarget target,
            HotkeyId id,
            GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            service.Register(new UnitTypeSelectHotkeyHandlerDelegate(id, prefab, target));
        }

#if UNITY_EDITOR
        void TryAssignDefaultPrefabsInEditor()
        {
            if (civilCentralPrefab != null && forgePrefab != null && barrackPrefab != null)
            {
                return;
            }

            civilCentralPrefab ??= UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/Buildings/civil_central/civil_central.prefab");
            forgePrefab ??= UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/Buildings/forge/forge.prefab");
            barrackPrefab ??= UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/Buildings/barrack/barrack.prefab");
        }
#endif
    }
}

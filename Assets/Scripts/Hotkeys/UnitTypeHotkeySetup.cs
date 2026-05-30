using GameDevTV.RTS.Hotkeys.Handlers;
using GameDevTV.RTS.Hotkeys.Targets;
using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// SRP: Gắn prefab archetype → HotkeyId và đăng ký delegate lên HotkeyService.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitTypeHotkeySetup : MonoBehaviour
    {
        [Header("Prefab archetype (UnitSO.Prefab)")]
        [SerializeField] GameObject workerPrefab;
        [SerializeField] GameObject warriorPrefab;
        [SerializeField] GameObject archerPrefab;
        [SerializeField] GameObject rockWarriorPrefab;

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
                    $"[{nameof(UnitTypeHotkeySetup)}] Thiếu HotkeySystem hoặc IHotkeyUnitTypeSelectTarget trên {name}.",
                    this);
                return;
            }

            RegisterIfAssigned(hotkeySystem.Service, target, HotkeyId.SelectWorkerOnScreen, workerPrefab);
            RegisterIfAssigned(hotkeySystem.Service, target, HotkeyId.SelectWarriorOnScreen, warriorPrefab);
            RegisterIfAssigned(hotkeySystem.Service, target, HotkeyId.SelectArcherOnScreen, archerPrefab);
            RegisterIfAssigned(hotkeySystem.Service, target, HotkeyId.SelectRockWarriorOnScreen, rockWarriorPrefab);
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
            if (workerPrefab != null
                && warriorPrefab != null
                && archerPrefab != null
                && rockWarriorPrefab != null)
            {
                return;
            }

            workerPrefab ??= UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/Unit/Worker 1.prefab");
            warriorPrefab ??= UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/Unit/Worrior.prefab");
            archerPrefab ??= UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/Unit/Archer.prefab");
            rockWarriorPrefab ??= UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefab/Unit/RockWarrior 1.prefab");
        }
#endif
    }
}

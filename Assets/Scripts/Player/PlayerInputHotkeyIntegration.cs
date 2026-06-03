using System;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Hotkeys;
using GameDevTV.RTS.Hotkeys.Handlers;
using GameDevTV.RTS.Hotkeys.Targets;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Gắn hotkey stack lên PlayerInput và bridge Esc/H vào gameplay.
    /// Tương thích MP: event bus theo LocalOwner; Stop relay qua PlayerInputNetworkBridge.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerInputHotkeyIntegration : MonoBehaviour,
        IHotkeyCancelTarget,
        IHotkeyStopUnitsTarget,
        IHotkeyUnitTypeSelectTarget,
        IHotkeyActionBarTarget,
        IHotkeyCameraTarget,
        IHotkeyDeleteSelectionTarget
    {
        PlayerInput playerInput;
        Owner logSubscribedOwner = Owner.Invalid;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void OnEnable()
        {
            LocalHumanOwnerService.LocalOwnerChanged += OnLocalOwnerChangedForLog;
            ResubscribeHotkeyLog(LocalHumanOwnerAccess.GetLocalOwnerOrDefault());
        }

        void OnDisable()
        {
            LocalHumanOwnerService.LocalOwnerChanged -= OnLocalOwnerChangedForLog;
            UnsubscribeHotkeyLog();
        }

        void OnLocalOwnerChangedForLog(Owner owner)
        {
            ResubscribeHotkeyLog(owner);
        }

        void ResubscribeHotkeyLog(Owner owner)
        {
            if (logSubscribedOwner == owner)
            {
                return;
            }

            UnsubscribeHotkeyLog();
            logSubscribedOwner = owner;
            Bus<HotkeyTriggeredEvent>.OnEvent[owner] += LogHotkeyTriggered;
        }

        void UnsubscribeHotkeyLog()
        {
            if (logSubscribedOwner == Owner.Invalid)
            {
                return;
            }

            Bus<HotkeyTriggeredEvent>.OnEvent[logSubscribedOwner] -= LogHotkeyTriggered;
            logSubscribedOwner = Owner.Invalid;
        }

        static void LogHotkeyTriggered(HotkeyTriggeredEvent evt)
        {
            Debug.Log($"[Hotkey] {evt.Id} ({evt.MatchedChord})");
        }
#endif

        void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            if (playerInput == null)
            {
                Debug.LogError($"{nameof(PlayerInputHotkeyIntegration)} requires {nameof(PlayerInput)}.", this);
                enabled = false;
            }
        }

        void Start()
        {
            if (playerInput == null)
            {
                return;
            }

            EnsureComponent<CancelSelectionHotkeyHandler>();
            EnsureComponent<StopUnitsHotkeyHandler>();
            EnsureComponent<ActionBarSlotHotkeyHandler>();
            EnsureComponent<CameraResetHotkeyHandler>();
            EnsureComponent<CameraFollowUnitHotkeyHandler>();
            EnsureComponent<DeleteSelectionHotkeyHandler>();
            EnsureComponent<DeleteSelectionImmediateHotkeyHandler>();
            EnsureComponent<HotkeySystem>();
            EnsureComponent<UnitTypeHotkeySetup>();
            EnsureComponent<BuildingTypeHotkeySetup>();
        }

        public void OnHotkeyActionBarSlot(int slotIndex)
        {
            playerInput?.OnHotkeyActionBarSlot(slotIndex);
        }

        public void OnHotkeySelectUnitType(GameObject referencePrefab, bool selectAllOnScreen)
        {
            playerInput?.OnHotkeySelectUnitType(referencePrefab, selectAllOnScreen);
        }

        public void OnHotkeyCancel(in HotkeyContext context)
        {
            playerInput?.CancelFromHotkey();
        }

        public void OnHotkeyStopUnits(in HotkeyContext context)
        {
            playerInput?.StopSelectedUnitsFromHotkey();
        }

        public void OnHotkeyResetCamera(in HotkeyContext context)
        {
            playerInput?.ResetCameraFromHotkey();
        }

        public void OnHotkeyFollowSelected(in HotkeyContext context)
        {
            playerInput?.FollowSelectedFromHotkey();
        }

        public void OnHotkeyDeleteSelection(in HotkeyContext context, bool immediate)
        {
            playerInput?.DeleteSelectionFromHotkey(immediate);
        }

        void EnsureComponent<T>() where T : Component
        {
            if (GetComponent<T>() == null)
            {
                gameObject.AddComponent<T>();
            }
        }
    }
}

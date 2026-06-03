using GameDevTV.RTS.Commands;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Game.Startup;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.Audio
{
    /// <summary>
    /// SRP: Map <see cref="Bus{T}"/> gameplay events → <see cref="AudioCueId"/> cho local player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAudioListener : MonoBehaviour
    {
        [SerializeField] Owner listenOwner = Owner.Player1;
        [SerializeField] bool playSupplySpend = true;
        [SerializeField] bool playVoiceOnUnitSelect = true;
        [SerializeField] bool playVoiceOnMoveCommand = true;
        [SerializeField] bool playVoiceOnAttackCommand = true;
        [SerializeField] float voiceCooldownSeconds = 0.35f;

        float lastVoiceTime = -999f;

        void Awake()
        {
            AudioBootstrap.EnsureExists();
        }

        /// <summary>
        /// Mục tiêu: Đổi phe lắng nghe khi LocalOwner đổi (MP / debug hotkey).
        /// Cách hoạt động: Unsubscribe owner cũ, gán mới, subscribe lại.
        /// </summary>
        public void SetListenOwner(Owner owner)
        {
            if (this == null)
            {
                return;
            }

            if (listenOwner == owner)
            {
                return;
            }

            if (isActiveAndEnabled)
            {
                UnsubscribeBus(listenOwner);
                listenOwner = owner;
                SubscribeBus(listenOwner);
                return;
            }

            listenOwner = owner;
        }

        void OnEnable()
        {
            GameplayStartupGate.Unlocked += HandleGameplayUnlocked;
            TrySubscribeWhenUnlocked();
        }

        void OnDisable()
        {
            GameplayStartupGate.Unlocked -= HandleGameplayUnlocked;
            UnsubscribeBus(listenOwner);
        }

        void HandleGameplayUnlocked() => TrySubscribeWhenUnlocked();

        void TrySubscribeWhenUnlocked()
        {
            if (!GameplayStartupGate.IsGameplayUnlocked
                && GameplayStartupScenes.IsActiveGameplayScene())
            {
                return;
            }

            SubscribeBus(listenOwner);
        }

        void SubscribeBus(Owner owner)
        {
            Bus<UnitSelectedEvent>.OnEvent[owner] += HandleUnitSelected;
            Bus<CommandSelectedEvent>.OnEvent[owner] += HandleCommandSelected;
            Bus<BuildingConstructStartedEvent>.OnEvent[owner] += HandleConstructionStarted;
            Bus<BuildingSpawnEvent>.OnEvent[owner] += HandleBuildingSpawn;
            Bus<UnitDeathEvent>.OnEvent[owner] += HandleUnitDeath;
            Bus<BuildingDeathEvent>.OnEvent[owner] += HandleBuildingDeath;
            Bus<UpgradeResearchedEvent>.OnEvent[owner] += HandleUpgrade;
            Bus<SupplyEvent>.OnEvent[owner] += HandleSupply;
        }

        void UnsubscribeBus(Owner owner)
        {
            Bus<UnitSelectedEvent>.OnEvent[owner] -= HandleUnitSelected;
            Bus<CommandSelectedEvent>.OnEvent[owner] -= HandleCommandSelected;
            Bus<BuildingConstructStartedEvent>.OnEvent[owner] -= HandleConstructionStarted;
            Bus<BuildingSpawnEvent>.OnEvent[owner] -= HandleBuildingSpawn;
            Bus<UnitDeathEvent>.OnEvent[owner] -= HandleUnitDeath;
            Bus<BuildingDeathEvent>.OnEvent[owner] -= HandleBuildingDeath;
            Bus<UpgradeResearchedEvent>.OnEvent[owner] -= HandleUpgrade;
            Bus<SupplyEvent>.OnEvent[owner] -= HandleSupply;
        }

        void HandleUnitSelected(UnitSelectedEvent evt)
        {
            if (evt.Unit == null)
            {
                return;
            }

            if (playVoiceOnUnitSelect && evt.Unit is AbstractUnit && TryPlayVoice(AudioCueId.VoiceSelect))
            {
                return;
            }

            AudioAccess.TryPlay(AudioCueId.UiSelect);
        }

        void HandleCommandSelected(CommandSelectedEvent evt)
        {
            if (evt.Command == null)
            {
                AudioAccess.TryPlay(AudioCueId.UiCommand);
                return;
            }

            if (evt.Command is AttackCommand && playVoiceOnAttackCommand && TryPlayVoice(AudioCueId.VoiceAttack))
            {
                return;
            }

            if (evt.Command is MoveCommand && playVoiceOnMoveCommand && TryPlayVoice(AudioCueId.VoiceMove))
            {
                return;
            }

            AudioAccess.TryPlay(AudioCueId.UiCommand);
        }

        void HandleConstructionStarted(BuildingConstructStartedEvent evt)
        {
            AudioAccess.TryPlay(AudioCueId.BuildStart);
        }

        void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            if (evt.Building == null || evt.Building.Progress.State != BuildingProgress.BuildingState.Completed)
            {
                return;
            }

            AudioAccess.TryPlay(AudioCueId.BuildComplete);
        }

        void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (evt.Unit == null || evt.Unit.Owner != listenOwner)
            {
                return;
            }

            AudioAccess.TryPlay(AudioCueId.UnitDeath);
        }

        void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            if (evt.Building == null || evt.Owner != listenOwner)
            {
                return;
            }

            if (CivilCentralUtility.IsCivilCentral(evt.Building))
            {
                return;
            }

            AudioAccess.TryPlay(AudioCueId.BuildingDeath);
        }

        void HandleUpgrade(UpgradeResearchedEvent evt)
        {
            AudioAccess.TryPlay(AudioCueId.UpgradeComplete);
        }

        void HandleSupply(SupplyEvent evt)
        {
            if (!playSupplySpend || evt.Amount >= 0)
            {
                return;
            }

            AudioAccess.TryPlay(AudioCueId.SupplySpend);
        }

        bool TryPlayVoice(AudioCueId cue)
        {
            if (Time.unscaledTime - lastVoiceTime < voiceCooldownSeconds)
            {
                return false;
            }

            if (!AudioAccess.TryPlay(cue))
            {
                return false;
            }

            lastVoiceTime = Time.unscaledTime;
            return true;
        }
    }
}

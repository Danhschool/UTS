using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.UI.GameEventLog
{
    /// <summary>
    /// Subscribes to <see cref="Bus{T}"/> events for Player1 and posts messages to <see cref="GameEventLog"/>.
    /// </summary>
    public class PlayerGameEventLogListener : MonoBehaviour
    {
        [SerializeField] private Owner listenOwner = Owner.Player1;
        [SerializeField] private bool logSupplyChanges = true;
        [Tooltip("Tắt thông báo \"Nhận +X\" khi gather; vẫn log chi tiêu tài nguyên (số âm).")]
        [SerializeField] private bool logSupplyGains = false;
        [SerializeField] private bool logCombatLosses = true;
        [SerializeField] private bool logBuildings = true;
        [SerializeField] private bool logUpgrades = true;

        private void Start()
        {
            GameEventLog.Post("Sự kiện game sẽ hiển thị tại đây.", GameEventLogCategory.Info);
        }

        private void OnEnable()
        {
            Bus<SupplyEvent>.OnEvent[listenOwner] += HandleSupply;
            Bus<BuildingSpawnEvent>.OnEvent[listenOwner] += HandleBuildingSpawn;
            Bus<BuildingDeathEvent>.OnEvent[listenOwner] += HandleBuildingDeath;
            Bus<BuildingDeathEvent>.RegisterForAll(HandleCivilCentralDestroyed);
            Bus<UnitDeathEvent>.OnEvent[listenOwner] += HandleUnitDeath;
            Bus<UpgradeResearchedEvent>.OnEvent[listenOwner] += HandleUpgrade;
            Bus<BuildingConstructStartedEvent>.OnEvent[listenOwner] += HandleConstructionStarted;
        }

        private void OnDisable()
        {
            Bus<SupplyEvent>.OnEvent[listenOwner] -= HandleSupply;
            Bus<BuildingSpawnEvent>.OnEvent[listenOwner] -= HandleBuildingSpawn;
            Bus<BuildingDeathEvent>.OnEvent[listenOwner] -= HandleBuildingDeath;
            Bus<BuildingDeathEvent>.UnregisterForAll(HandleCivilCentralDestroyed);
            Bus<UnitDeathEvent>.OnEvent[listenOwner] -= HandleUnitDeath;
            Bus<UpgradeResearchedEvent>.OnEvent[listenOwner] -= HandleUpgrade;
            Bus<BuildingConstructStartedEvent>.OnEvent[listenOwner] -= HandleConstructionStarted;
        }

        private void HandleSupply(SupplyEvent evt)
        {
            if (!logSupplyChanges)
            {
                return;
            }

            if (!logSupplyGains && evt.Amount > 0)
            {
                return;
            }

            if (GameEventLogMessageFormatter.TryFormatSupply(evt, out string message, out GameEventLogCategory category))
            {
                GameEventLog.Post(message, category);
            }
        }

        private void HandleBuildingSpawn(BuildingSpawnEvent evt)
        {
            if (!logBuildings || evt.Building == null)
            {
                return;
            }

            if (evt.Building.Progress.State != BuildingProgress.BuildingState.Completed)
            {
                return;
            }

            GameEventLog.Post(
                GameEventLogMessageFormatter.FormatBuildingCompleted(evt.Building),
                GameEventLogCategory.Build);
        }

        private void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            if (!logBuildings || evt.Building == null)
            {
                return;
            }

            if (CivilCentralUtility.IsCivilCentral(evt.Building))
            {
                return;
            }

            GameEventLog.Post(
                GameEventLogMessageFormatter.FormatBuildingLost(evt.Building),
                GameEventLogCategory.Combat);
        }

        private void HandleCivilCentralDestroyed(BuildingDeathEvent evt)
        {
            if (!logBuildings || evt.Building == null || !CivilCentralUtility.IsCivilCentral(evt.Building))
            {
                return;
            }

            GameEventLog.Post(
                GameEventLogMessageFormatter.FormatCivilCentralDestroyed(evt.Owner),
                GameEventLogCategory.GameOver);
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (!logCombatLosses || evt.Unit == null || evt.Unit.Owner != listenOwner)
            {
                return;
            }

            GameEventLog.Post(
                GameEventLogMessageFormatter.FormatUnitLost(evt.Unit),
                GameEventLogCategory.Combat);
        }

        private void HandleUpgrade(UpgradeResearchedEvent evt)
        {
            if (!logUpgrades || evt.Upgrade == null)
            {
                return;
            }

            GameEventLog.Post(
                GameEventLogMessageFormatter.FormatUpgrade(evt.Upgrade),
                GameEventLogCategory.Info);
        }

        private void HandleConstructionStarted(BuildingConstructStartedEvent evt)
        {
            if (!logBuildings)
            {
                return;
            }

            GameEventLog.Post(
                GameEventLogMessageFormatter.FormatConstructionStarted(),
                GameEventLogCategory.Build);
        }
    }
}

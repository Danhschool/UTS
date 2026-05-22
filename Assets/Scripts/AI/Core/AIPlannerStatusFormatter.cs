using System.Text;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Một dòng trạng thái AI cho Game Event Log (thay thông báo gather từng lần).
    /// </summary>
    public static class AIPlannerStatusFormatter
    {
        /// <summary>
        /// Mục tiêu: Tóm tắt hành vi AI tick hiện tại cho người chơi đọc nhanh.
        /// Cách hoạt động: Đếm worker/quân, đọc rally phase, ghi tài nguyên S/W/F.
        /// </summary>
        public static string Format(AIWorldStateSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return string.Empty;
            }

            StringBuilder sb = new(160);
            sb.Append("[AI ").Append(snapshot.Owner).Append("] ");
            sb.Append("S/W/F=").Append(snapshot.Stone).Append('/').Append(snapshot.Wood).Append('/').Append(snapshot.Food);
            sb.Append(" | Dân=").Append(snapshot.Workers.Count);
            sb.Append(" Quân=").Append(snapshot.MilitaryUnits.Count);

            AppendMilitaryStatus(sb, snapshot);
            AppendExpansionStatus(sb, snapshot);
            AppendEconomyStatus(sb, snapshot);
            AppendBuildStatus(sb, snapshot);
            AppendDefenseRingTowerStatus(sb, snapshot);
            return sb.ToString();
        }

        private static void AppendDefenseRingTowerStatus(StringBuilder sb, AIWorldStateSnapshot snapshot)
        {
            AIMilitarySettings military = AIMilitarySettings.Default;
            if (!military.EnableDefenseRingExpansion)
            {
                return;
            }

            int towers = AIInfraBuildUtility.CountInfraBuildings(
                snapshot,
                AIInfraBuildUtility.DefenseTowerDisplayName);
            int required = AIMilitaryDefenseRingTowerArmyGate.GetRequiredLifetimeMilitarySpawned(
                snapshot,
                military);
            int have = AIMilitaryDefenseRingTowerArmyGate.GetEffectiveMilitaryCountForTowerGate(snapshot);
            bool canPlace = AIMilitaryDefenseRingTowerArmyGate.CanPlaceNextDefenseTower(snapshot, military);
            sb.Append(" | Tháp vòng ").Append(towers)
                .Append(" quân ").Append(have)
                .Append('/')
                .Append(required);
            if (!canPlace)
            {
                sb.Append(" (chờ quân→train sau)");
            }
            else if (AIMilitaryDefenseRingTowerArmyGate.ShouldDeferBarrackTrainingForTower(
                         snapshot,
                         military,
                         defenseRingMode: true))
            {
                sb.Append(" (ưu tiên xây)");
            }
            else if (AIInfraBuildUtility.HasPendingInfraBuild(
                         snapshot,
                         AIInfraBuildUtility.DefenseTowerDisplayName))
            {
                sb.Append(" (đang xây)");
            }
            else
            {
                int ring = AIMilitaryDefenseRingPlanner.GetCurrentRingIndex(snapshot.Owner);
                int unlocked = AIMilitaryDefenseRingPlanner.GetMaxUnlockedRingIndex(snapshot.Owner);
                sb.Append(" v").Append(ring + 1).Append('/').Append(unlocked + 1);
            }
        }

        private static void AppendMilitaryStatus(StringBuilder sb, AIWorldStateSnapshot snapshot)
        {
            if (snapshot.MilitaryUnits.Count == 0)
            {
                return;
            }

            AIMilitaryArmyAssemblyTracker.ArmyAssemblyPhase assembly =
                AIMilitaryArmyAssemblyTracker.GetPhase(snapshot.Owner);
            if (assembly == AIMilitaryArmyAssemblyTracker.ArmyAssemblyPhase.Forming)
            {
                sb.Append(" | Đội: gom ~10 (formation)");
            }
            else if (assembly == AIMilitaryArmyAssemblyTracker.ArmyAssemblyPhase.Ready)
            {
                sb.Append(" | Đội: sẵn sàng");
            }

            AIMilitaryRallySessionTracker.RallyPhase phase =
                AIMilitaryRallySessionTracker.GetPhase(snapshot.Owner);
            if (phase == AIMilitaryRallySessionTracker.RallyPhase.Idle)
            {
                return;
            }

            sb.Append(" | Quân sự: ");
            sb.Append(phase switch
            {
                AIMilitaryRallySessionTracker.RallyPhase.Regrouping => "Tập hợp đội",
                AIMilitaryRallySessionTracker.RallyPhase.Attacking => "Attack cả đội",
                _ => "—"
            });
        }

        private static void AppendExpansionStatus(StringBuilder sb, AIWorldStateSnapshot snapshot)
        {
            AIMilitarySettings military = AIMilitarySettings.Default;
            if (AIMilitaryExpansionPlanner.IsMilitaryInfraBootstrapPhase(snapshot, military))
            {
                int barracks = AIInfraBuildUtility.CountInfraBuildings(snapshot, AIInfraBuildUtility.BarrackDisplayName);
                int towers = AIInfraBuildUtility.CountInfraBuildings(snapshot, AIInfraBuildUtility.DefenseTowerDisplayName);
                sb.Append(" | XP đầu: Barrack ").Append(barracks)
                    .Append('/').Append(military.EarlyGameTargetBarrackCount)
                    .Append(" Tháp ").Append(towers)
                    .Append('/').Append(military.EarlyGameTargetDefenseTowerCount);
                return;
            }

            if (snapshot.MilitaryUnits.Count >= 10)
            {
                sb.Append(" | Mở rộng quân");
            }
            else if (AIMilitaryExpansionPlanner.ShouldAllowBarrackTraining(snapshot, military))
            {
                sb.Append(" | Train quân Barrack");
            }
            else if (AIMilitaryExpansionPlanner.CountOperationalBarracks(snapshot) >= 1
                && !AIMilitaryExpansionPlanner.HasMilitaryExpansionResourceReserve(snapshot, military))
            {
                sb.Append(" | Chờ S/W train (≥")
                    .Append(military.MinStoneBeforeMilitaryExpansion)
                    .Append('/')
                    .Append(military.MinWoodBeforeMilitaryExpansion)
                    .Append(')');
            }
        }

        private static void AppendEconomyStatus(StringBuilder sb, AIWorldStateSnapshot snapshot)
        {
            int gathering = 0;
            int returning = 0;
            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                Worker worker = snapshot.Workers[i];
                if (worker == null)
                {
                    continue;
                }

                if (worker.IsGathering)
                {
                    gathering++;
                }
                else if (worker.IsGatheringOrReturning && !worker.IsGathering)
                {
                    returning++;
                }
            }

            if (gathering > 0)
            {
                sb.Append(" | Thu thập: ").Append(gathering).Append(" worker");
            }

            int staleGather = 0;
            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                Worker worker = snapshot.Workers[i];
                if (worker != null && worker.HasStaleGatherCommand)
                {
                    staleGather++;
                }
            }

            if (staleGather > 0)
            {
                sb.Append(" | Gather kẹt: ").Append(staleGather);
            }

            if (returning > 0)
            {
                sb.Append(" | Mang về kho: ").Append(returning).Append(" worker");
            }
        }

        private static void AppendBuildStatus(StringBuilder sb, AIWorldStateSnapshot snapshot)
        {
            int buildingWorkers = 0;
            for (int i = 0; i < snapshot.Workers.Count; i++)
            {
                Worker worker = snapshot.Workers[i];
                if (worker != null && worker.TryGetCommittedBuildBuildingName(out _))
                {
                    buildingWorkers++;
                }
            }

            if (buildingWorkers > 0)
            {
                sb.Append(" | Xây: ").Append(buildingWorkers).Append(" worker");
            }

            int sites = 0;
            for (int i = 0; i < snapshot.Buildings.Count; i++)
            {
                BaseBuilding building = snapshot.Buildings[i];
                if (building?.Progress.State == BuildingProgress.BuildingState.Building)
                {
                    sites++;
                }
            }

            if (sites > 0)
            {
                sb.Append(" | Công trường: ").Append(sites);
            }
        }
    }
}

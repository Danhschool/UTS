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
            AppendEconomyStatus(sb, snapshot);
            AppendBuildStatus(sb, snapshot);
            return sb.ToString();
        }

        private static void AppendMilitaryStatus(StringBuilder sb, AIWorldStateSnapshot snapshot)
        {
            if (snapshot.MilitaryUnits.Count == 0)
            {
                return;
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
                AIMilitaryRallySessionTracker.RallyPhase.Regrouping => "Tập hợp (gần nhà)",
                AIMilitaryRallySessionTracker.RallyPhase.Attacking => "Tấn công (Attack)",
                _ => "—"
            });
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

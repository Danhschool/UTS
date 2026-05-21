using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Chọn train Barrack theo <b>tỷ lệ %</b> trên tổng quân (active + queue) — deficit = targetShare − currentShare.
    /// </summary>
    public static class AIMilitaryUnitMixPlanner
    {
        private const int MaxMixSlots = 3;
        private static readonly AIMilitaryUnitMixSlot[] SlotScratch = new AIMilitaryUnitMixSlot[MaxMixSlots];
        private static readonly int[] CountScratch = new int[MaxMixSlots];

        /// <summary>
        /// Mục tiêu: Lệnh train loại unit đang thiếu so với tỷ lệ % cấu hình.
        /// Cách hoạt động: Đếm tổng theo slot; chọn slot có deficit % lớn nhất và CanEnqueueTrain.
        /// </summary>
        public static BuildUnitCommand PickTrainCommand(
            AIWorldStateSnapshot snapshot,
            BaseBuilding barrack,
            AIMilitarySettings settings,
            ref int slot0Count,
            ref int slot1Count,
            ref int slot2Count)
        {
            int activeCount = CollectActiveSlots(settings, SlotScratch);
            if (activeCount == 0 || barrack == null || snapshot == null)
            {
                return null;
            }

            int totalCommitted = GetTotalFromSlotCounts(activeCount, slot0Count, slot1Count, slot2Count);
            float weightSum = SumWeights(SlotScratch, activeCount);

            BuildUnitCommand bestCommand = null;
            float bestDeficit = float.MinValue;

            for (int i = 0; i < activeCount; i++)
            {
                BuildUnitCommand command = SlotScratch[i].TrainCommand;
                if (!AIMilitaryConfigResolver.CanEnqueueTrain(snapshot, barrack, command))
                {
                    continue;
                }

                float targetShare = SlotScratch[i].SpawnWeight / weightSum;
                int slotCount = GetSlotCount(activeCount, i, slot0Count, slot1Count, slot2Count);
                float currentShare = totalCommitted > 0 ? slotCount / (float)totalCommitted : 0f;
                float deficit = targetShare - currentShare;
                if (totalCommitted == 0)
                {
                    deficit = targetShare;
                }

                if (deficit <= bestDeficit)
                {
                    continue;
                }

                bestDeficit = deficit;
                bestCommand = command;
            }

            return bestCommand ?? TryPickFallbackTrainCommand(snapshot, barrack, activeCount, -1);
        }

        /// <summary>
        /// Mục tiêu: Đếm quân + queue trước vòng train (một lần mỗi tick).
        /// Cách hoạt động: Quân sống theo UnitSO; cộng mọi Barrack Queue + SOBeingBuilt.
        /// </summary>
        public static void CountCommittedMilitaryByMixSlot(
            AIWorldStateSnapshot snapshot,
            IReadOnlyList<BaseBuilding> barracks,
            AIMilitarySettings settings,
            out int slot0Count,
            out int slot1Count,
            out int slot2Count)
        {
            slot0Count = slot1Count = slot2Count = 0;
            int activeCount = CollectActiveSlots(settings, SlotScratch);
            if (activeCount == 0 || snapshot == null)
            {
                return;
            }

            for (int i = 0; i < activeCount; i++)
            {
                CountScratch[i] = 0;
            }

            for (int u = 0; u < snapshot.MilitaryUnits.Count; u++)
            {
                AbstractUnit unit = snapshot.MilitaryUnits[u];
                if (unit?.UnitSO == null)
                {
                    continue;
                }

                int slotIndex = FindSlotIndexForUnit(SlotScratch, activeCount, unit.UnitSO);
                if (slotIndex >= 0)
                {
                    CountScratch[slotIndex]++;
                }
            }

            if (barracks != null)
            {
                for (int b = 0; b < barracks.Count; b++)
                {
                    AddBarrackQueueToCountScratch(barracks[b], SlotScratch, activeCount);
                }
            }

            slot0Count = activeCount > 0 ? CountScratch[0] : 0;
            slot1Count = activeCount > 1 ? CountScratch[1] : 0;
            slot2Count = activeCount > 2 ? CountScratch[2] : 0;
        }

        /// <summary>
        /// Mục tiêu: Sau enqueue train, cập nhật tỷ lệ % cho lần pick tiếp theo trong cùng tick.
        /// </summary>
        public static void IncrementPendingTrainSlotCount(
            int slotIndex,
            ref int slot0Count,
            ref int slot1Count,
            ref int slot2Count)
        {
            switch (slotIndex)
            {
                case 0:
                    slot0Count++;
                    break;
                case 1:
                    slot1Count++;
                    break;
                case 2:
                    slot2Count++;
                    break;
            }
        }

        public static void AppendTrainCommandsFromMix(AIMilitarySettings settings, List<BuildUnitCommand> output)
        {
            if (output == null || settings == null)
            {
                return;
            }

            int activeCount = CollectActiveSlots(settings, SlotScratch);
            for (int i = 0; i < activeCount; i++)
            {
                BuildUnitCommand command = SlotScratch[i].TrainCommand;
                if (command != null && !output.Contains(command))
                {
                    output.Add(command);
                }
            }
        }

        public static int FindMixSlotIndexForCommand(AIMilitarySettings settings, BuildUnitCommand command)
        {
            if (command == null || settings == null)
            {
                return -1;
            }

            int activeCount = CollectActiveSlots(settings, SlotScratch);
            for (int i = 0; i < activeCount; i++)
            {
                if (SlotScratch[i].TrainCommand == command)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int GetTotalFromSlotCounts(int activeCount, int s0, int s1, int s2)
        {
            int total = s0;
            if (activeCount > 1)
            {
                total += s1;
            }

            if (activeCount > 2)
            {
                total += s2;
            }

            return total;
        }

        private static int GetSlotCount(int activeCount, int index, int s0, int s1, int s2) =>
            index switch
            {
                0 => s0,
                1 => activeCount > 1 ? s1 : 0,
                _ => activeCount > 2 ? s2 : 0
            };

        private static BuildUnitCommand TryPickFallbackTrainCommand(
            AIWorldStateSnapshot snapshot,
            BaseBuilding barrack,
            int activeCount,
            int skipIndex)
        {
            for (int i = 0; i < activeCount; i++)
            {
                if (i == skipIndex)
                {
                    continue;
                }

                BuildUnitCommand candidate = SlotScratch[i].TrainCommand;
                if (AIMilitaryConfigResolver.CanEnqueueTrain(snapshot, barrack, candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static int CollectActiveSlots(AIMilitarySettings settings, AIMilitaryUnitMixSlot[] buffer)
        {
            int count = 0;
            TryAddSlot(settings.UnitMixSlot1, buffer, ref count);
            TryAddSlot(settings.UnitMixSlot2, buffer, ref count);
            TryAddSlot(settings.UnitMixSlot3, buffer, ref count);
            return count;
        }

        private static void TryAddSlot(AIMilitaryUnitMixSlot slot, AIMilitaryUnitMixSlot[] buffer, ref int count)
        {
            if (count >= MaxMixSlots || !slot.IsAssigned)
            {
                return;
            }

            buffer[count++] = slot;
        }

        private static float SumWeights(AIMilitaryUnitMixSlot[] slots, int count)
        {
            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                sum += slots[i].SpawnWeight;
            }

            return Mathf.Max(sum, 0.001f);
        }

        private static void AddBarrackQueueToCountScratch(
            BaseBuilding barrack,
            AIMilitaryUnitMixSlot[] slots,
            int slotCount)
        {
            if (barrack?.Queue == null)
            {
                return;
            }

            UnlockableSO[] queue = barrack.Queue;
            for (int q = 0; q < queue.Length; q++)
            {
                if (queue[q] is not AbstractUnitSO unitSo)
                {
                    continue;
                }

                int slotIndex = FindSlotIndexForUnit(slots, slotCount, unitSo);
                if (slotIndex >= 0)
                {
                    CountScratch[slotIndex]++;
                }
            }

            if (barrack.SOBeingBuilt is AbstractUnitSO buildingUnit)
            {
                int slotIndex = FindSlotIndexForUnit(slots, slotCount, buildingUnit);
                if (slotIndex >= 0)
                {
                    CountScratch[slotIndex]++;
                }
            }
        }

        private static int FindSlotIndexForUnit(
            AIMilitaryUnitMixSlot[] slots,
            int slotCount,
            AbstractUnitSO unitSo)
        {
            for (int i = 0; i < slotCount; i++)
            {
                if (slots[i].TrainCommand?.Unit == unitSo)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Vòng tháp 100m +100m/vòng; nhịp 5 phút chỉ mở khóa thêm vòng (bán kính), không gate đặt tháp.
    /// </summary>
    public static class AIMilitaryDefenseRingPlanner
    {
        private sealed class RingState
        {
            public int CurrentRingIndex;
            public int NextTowerSlot;
            public int MaxUnlockedRingIndex;
            public float LastRingExpansionTime;
        }

        private static readonly System.Collections.Generic.Dictionary<int, RingState> StatesByOwner = new(4);

        public static float GetRingRadius(AIMilitarySettings settings, int ringIndex)
        {
            if (settings == null)
            {
                return 100f;
            }

            ringIndex = Mathf.Clamp(ringIndex, 0, settings.DefenseRingMaxRings - 1);
            return settings.DefenseRingInnerRadius + ringIndex * settings.DefenseRingSpacing;
        }

        public static int GetCurrentRingIndex(Owner owner) =>
            StatesByOwner.TryGetValue((int)owner, out RingState state) ? state.CurrentRingIndex : 0;

        public static int GetMaxUnlockedRingIndex(Owner owner) =>
            StatesByOwner.TryGetValue((int)owner, out RingState state) ? state.MaxUnlockedRingIndex : 0;

        /// <summary>
        /// Mục tiêu: Mỗi N giây (5 phút) mở thêm một vòng bán kính — thêm chỗ candidate, không phải cửa sổ đặt tháp.
        /// </summary>
        public static void TickRingRadiusExpansion(Owner owner, AIMilitarySettings settings)
        {
            if (settings == null || !settings.EnableDefenseRingExpansion)
            {
                return;
            }

            RingState state = GetOrCreate(owner);
            float interval = Mathf.Max(60f, settings.TowerExpansionIntervalSeconds);
            if (Time.time < state.LastRingExpansionTime + interval)
            {
                return;
            }

            state.LastRingExpansionTime = Time.time;
            if (state.MaxUnlockedRingIndex < settings.DefenseRingMaxRings - 1)
            {
                state.MaxUnlockedRingIndex++;
            }
        }

        public static bool IsRingUnlocked(Owner owner, int ringIndex) =>
            ringIndex <= GetMaxUnlockedRingIndex(owner);

        /// <summary>
        /// Mục tiêu: Slot tiếp theo trên vòng hiện tại; hết slot → vòng sau (max 5).
        /// </summary>
        public static bool TryAdvanceTowerSlot(AIMilitarySettings settings, Owner owner, out int ringIndex, out int slotIndex)
        {
            ringIndex = 0;
            slotIndex = 0;
            if (settings == null)
            {
                return false;
            }

            RingState state = GetOrCreate(owner);
            ClampRingIndexToUnlocked(state, settings);
            ringIndex = state.CurrentRingIndex;
            slotIndex = state.NextTowerSlot;
            state.NextTowerSlot++;
            if (state.NextTowerSlot >= settings.TowersPerRing)
            {
                state.NextTowerSlot = 0;
                int maxUnlockedRing = Mathf.Min(
                    settings.DefenseRingMaxRings - 1,
                    state.MaxUnlockedRingIndex);
                if (state.CurrentRingIndex < maxUnlockedRing)
                {
                    state.CurrentRingIndex++;
                }
                else
                {
                    state.CurrentRingIndex = 0;
                }
            }

            return true;
        }

        public static bool TryGetCurrentTowerSlot(Owner owner, out int ringIndex, out int slotIndex)
        {
            ringIndex = 0;
            slotIndex = 0;
            if (!StatesByOwner.TryGetValue((int)owner, out RingState state))
            {
                return false;
            }

            ringIndex = Mathf.Min(state.CurrentRingIndex, state.MaxUnlockedRingIndex);
            slotIndex = state.NextTowerSlot;
            return true;
        }

        private static void ClampRingIndexToUnlocked(RingState state, AIMilitarySettings settings)
        {
            int maxUnlockedRing = Mathf.Min(
                settings.DefenseRingMaxRings - 1,
                state.MaxUnlockedRingIndex);
            state.CurrentRingIndex = Mathf.Clamp(state.CurrentRingIndex, 0, maxUnlockedRing);
        }

        public static bool HasReachedMaxRings(AIMilitarySettings settings, Owner owner)
        {
            if (settings == null)
            {
                return true;
            }

            RingState state = GetOrCreate(owner);
            return state.CurrentRingIndex >= settings.DefenseRingMaxRings - 1
                && state.NextTowerSlot >= settings.TowersPerRing;
        }

        private static RingState GetOrCreate(Owner owner)
        {
            int key = (int)owner;
            if (!StatesByOwner.TryGetValue(key, out RingState state))
            {
                state = new RingState { LastRingExpansionTime = Time.time };
                StatesByOwner[key] = state;
            }

            return state;
        }
    }
}

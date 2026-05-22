using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Thiếu tài nguyên khi spam tháp nhiều lần → yêu cầu thêm worker / Corral.
    /// </summary>
    public static class AIMilitaryDefenseRingEconomyRecovery
    {
        private sealed class RecoveryState
        {
            public int ConsecutiveAffordFailures;
            public bool BoostRequested;
        }

        private static readonly System.Collections.Generic.Dictionary<int, RecoveryState> StatesByOwner = new(4);

        public static void RecordAffordFailure(Owner owner, int threshold)
        {
            RecoveryState state = GetOrCreate(owner);
            state.ConsecutiveAffordFailures++;
            if (state.ConsecutiveAffordFailures >= Mathf.Max(2, threshold))
            {
                state.BoostRequested = true;
            }
        }

        public static void ClearAffordFailures(Owner owner)
        {
            if (StatesByOwner.TryGetValue((int)owner, out RecoveryState state))
            {
                state.ConsecutiveAffordFailures = 0;
            }
        }

        public static bool ShouldRequestEconomyBoost(Owner owner) =>
            StatesByOwner.TryGetValue((int)owner, out RecoveryState state) && state.BoostRequested;

        public static void ClearBoostRequest(Owner owner)
        {
            if (StatesByOwner.TryGetValue((int)owner, out RecoveryState state))
            {
                state.BoostRequested = false;
                state.ConsecutiveAffordFailures = 0;
            }
        }

        public static bool NeedsCorral(AIWorldStateSnapshot snapshot) =>
            snapshot != null
            && AIInfraBuildUtility.HasInfraPresent(snapshot, AIInfraBuildUtility.StoreHouseDisplayName)
            && !AIInfraBuildUtility.HasInfraPresent(snapshot, AIInfraBuildUtility.CorralDisplayName);

        private static RecoveryState GetOrCreate(Owner owner)
        {
            int key = (int)owner;
            if (!StatesByOwner.TryGetValue(key, out RecoveryState state))
            {
                state = new RecoveryState();
                StatesByOwner[key] = state;
            }

            return state;
        }
    }
}

using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Sau khi scout thấy địch không còn tụ — đợi khám phá + xây (mặc định 2 phút) rồi tổng tấn công về CC địch.
    /// </summary>
    public static class AIMilitaryPostContactOffensePlanner
    {
        private sealed class PostContactState
        {
            public bool HadHostileContact;
            public bool ExploreBuildBufferActive;
            public float ExploreBuildBufferEndTime;
            public bool PostBufferOffensiveLaunched;
        }

        private static readonly System.Collections.Generic.Dictionary<int, PostContactState> StatesByOwner =
            new(4);

        /// <summary>
        /// Mục tiêu: Cập nhật trạng thái contact / buffer mỗi tick military.
        /// Cách hoạt động: Có địch → reset buffer; mất địch sau khi đã contact → bật timer 2 phút.
        /// </summary>
        public static void SyncPostContactState(
            Owner aiOwner,
            float exploreBuildBufferSeconds,
            bool hasRallyTarget,
            bool rallySessionActive,
            bool threatsNearFriendlyCc)
        {
            if (exploreBuildBufferSeconds <= 0f)
            {
                return;
            }

            PostContactState state = GetOrCreate(aiOwner);
            bool hostileNow = hasRallyTarget || rallySessionActive || threatsNearFriendlyCc;

            if (hostileNow)
            {
                state.HadHostileContact = true;
                state.ExploreBuildBufferActive = false;
                state.PostBufferOffensiveLaunched = false;
                return;
            }

            if (!state.HadHostileContact)
            {
                return;
            }

            if (state.PostBufferOffensiveLaunched)
            {
                return;
            }

            if (!state.ExploreBuildBufferActive)
            {
                state.ExploreBuildBufferActive = true;
                state.ExploreBuildBufferEndTime = Time.time + exploreBuildBufferSeconds;
                AIMilitaryRallySessionTracker.Clear(aiOwner);
            }
        }

        public static bool IsInExploreBuildBuffer(Owner aiOwner) =>
            StatesByOwner.TryGetValue((int)aiOwner, out PostContactState state)
            && state.ExploreBuildBufferActive
            && Time.time < state.ExploreBuildBufferEndTime
            && !state.PostBufferOffensiveLaunched;

        public static bool ShouldExecutePostBufferOffensive(Owner aiOwner) =>
            StatesByOwner.TryGetValue((int)aiOwner, out PostContactState state)
            && state.ExploreBuildBufferActive
            && !state.PostBufferOffensiveLaunched
            && Time.time >= state.ExploreBuildBufferEndTime;

        public static void MarkPostBufferOffensiveLaunched(Owner aiOwner)
        {
            if (!StatesByOwner.TryGetValue((int)aiOwner, out PostContactState state))
            {
                return;
            }

            state.PostBufferOffensiveLaunched = true;
            state.ExploreBuildBufferActive = false;
        }

        public static void Clear(Owner aiOwner) => StatesByOwner.Remove((int)aiOwner);

        private static PostContactState GetOrCreate(Owner aiOwner)
        {
            int key = (int)aiOwner;
            if (!StatesByOwner.TryGetValue(key, out PostContactState state))
            {
                state = new PostContactState();
                StatesByOwner[key] = state;
            }

            return state;
        }
    }
}

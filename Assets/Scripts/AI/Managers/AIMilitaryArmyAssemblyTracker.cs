using System.Collections.Generic;

using GameDevTV.RTS.Commands;

using GameDevTV.RTS.Units;

using UnityEngine;



namespace GameDevTV.RTS.AI

{

    /// <summary>

    /// SRP: Đội ~N lính tập hợp formation xa CC trước tấn công tổng (không dùng cho patrol lẻ).

    /// </summary>

    public static class AIMilitaryArmyAssemblyTracker

    {

        public enum ArmyAssemblyPhase

        {

            Idle,

            Forming,

            Ready

        }



        private sealed class AssemblySession

        {

            public ArmyAssemblyPhase Phase;

            public Vector3 StagingPoint;

            public float StagingBearingRadians;

            public int LastFormationArmyCount;

        }



        private static readonly Dictionary<int, AssemblySession> SessionsByOwner = new(4);



        /// <summary>

        /// Mục tiêu: Điểm tập hợp xa Civil Central (không sát Barrack).

        /// Cách hoạt động: Offset theo góc cố định mỗi session × bán kính đầy đủ từ CC.

        /// </summary>

        public static Vector3 ComputeStagingPoint(

            Vector3 civilCentralPosition,

            float stagingRadiusFromCc,

            float bearingRadians)

        {

            float dist = Mathf.Max(12f, stagingRadiusFromCc);

            Vector3 point = civilCentralPosition + new Vector3(

                Mathf.Cos(bearingRadians) * dist,

                0f,

                Mathf.Sin(bearingRadians) * dist);

            point.y = civilCentralPosition.y;

            return point;

        }



        /// <summary>

        /// Mục tiêu: Cập nhật phase — đủ headcount quân → Ready (scout); chưa đủ → Forming + staging.

        /// Cách hoạt động: totalMilitaryCount ≥ squadSize → Ready; ngược lại ≥ minToStage → Forming.

        /// </summary>

        public static ArmyAssemblyPhase Sync(

            Owner aiOwner,

            Vector3 civilCentralPosition,

            IReadOnlyList<AbstractUnit> army,

            int totalMilitaryCount,

            AIMilitarySettings settings,

            MoveCommand moveCommand)

        {

            if (settings == null || !settings.RequireArmyAssemblyBeforePatrol)

            {

                return ArmyAssemblyPhase.Ready;

            }



            int squadSize = settings.UnifiedArmySquadSize;

            int minToStage = Mathf.Max(1, settings.MinUnitsToBeginStagingAssembly);

            if (army == null || army.Count < minToStage || totalMilitaryCount < minToStage)

            {

                Clear(aiOwner);

                return ArmyAssemblyPhase.Idle;

            }



            int key = (int)aiOwner;

            if (!SessionsByOwner.TryGetValue(key, out AssemblySession session))

            {

                session = new AssemblySession

                {

                    Phase = ArmyAssemblyPhase.Forming,

                    StagingBearingRadians = Random.Range(0f, Mathf.PI * 2f)

                };

                SessionsByOwner[key] = session;

            }



            session.StagingPoint = ComputeStagingPoint(

                civilCentralPosition,

                settings.ArmyAssemblyStagingRadiusFromCc,

                session.StagingBearingRadians);



            bool hasFullHeadcount = totalMilitaryCount >= squadSize && army.Count >= squadSize;

            if (hasFullHeadcount)

            {

                session.Phase = ArmyAssemblyPhase.Ready;

                return session.Phase;

            }



            if (moveCommand != null

                && army.Count >= squadSize

                && AIMilitaryArmySquadExecutor.IsArmyGatheredAtStaging(

                    army,

                    session.StagingPoint,

                    moveCommand,

                    settings.ArmyAssemblyGatherRadius,

                    settings.ArmyAssemblyRequiredFraction))

            {

                session.Phase = ArmyAssemblyPhase.Ready;

                return session.Phase;

            }



            session.Phase = ArmyAssemblyPhase.Forming;

            return session.Phase;

        }



        /// <summary>

        /// Mục tiêu: Lính mới spawn — formation lại để có ô trống trong lưới.

        /// Cách hoạt động: So sánh army.Count với lần formation trước; tăng → true một tick.

        /// </summary>

        public static bool ConsumeFormationRefreshForGrowingArmy(Owner aiOwner, int currentArmyCount)

        {

            if (currentArmyCount <= 0)

            {

                return false;

            }



            int key = (int)aiOwner;

            if (!SessionsByOwner.TryGetValue(key, out AssemblySession session))

            {

                return true;

            }



            bool grew = currentArmyCount > session.LastFormationArmyCount;

            session.LastFormationArmyCount = currentArmyCount;

            return grew;

        }



        /// <summary>

        /// Mục tiêu: Luôn có staging khi đang Forming (kể cả chưa đủ 10 lính).

        /// </summary>

        public static bool TryGetStagingPoint(

            Owner aiOwner,

            Vector3 civilCentralPosition,

            AIMilitarySettings settings,

            out Vector3 stagingPoint)

        {

            float radius = settings != null ? settings.ArmyAssemblyStagingRadiusFromCc : 48f;

            float bearing = 0.785398163f;

            if (SessionsByOwner.TryGetValue((int)aiOwner, out AssemblySession session)

                && session.Phase != ArmyAssemblyPhase.Idle)

            {

                stagingPoint = session.StagingPoint;

                return true;

            }



            stagingPoint = ComputeStagingPoint(civilCentralPosition, radius, bearing);

            return settings != null && settings.RequireArmyAssemblyBeforePatrol;

        }



        public static bool TryGetStagingPoint(Owner aiOwner, out Vector3 stagingPoint)

        {

            stagingPoint = default;

            if (!SessionsByOwner.TryGetValue((int)aiOwner, out AssemblySession session)

                || session.Phase == ArmyAssemblyPhase.Idle)

            {

                return false;

            }



            stagingPoint = session.StagingPoint;

            return true;

        }



        public static ArmyAssemblyPhase GetPhase(Owner aiOwner) =>

            SessionsByOwner.TryGetValue((int)aiOwner, out AssemblySession session)

                ? session.Phase

                : ArmyAssemblyPhase.Idle;



        public static void Clear(Owner aiOwner) => SessionsByOwner.Remove((int)aiOwner);

    }

}



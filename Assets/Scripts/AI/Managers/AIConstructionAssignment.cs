using System.Collections.Generic;

using GameDevTV.RTS.Units;



namespace GameDevTV.RTS.AI

{

    /// <summary>

    /// SRP: Worker được base gán build — economy bỏ qua gather cho đến khi hết cam kết xây (đi tới site / đang xây).

    /// </summary>

    public static class AIConstructionAssignment

    {

        /// <summary>InstanceID worker đang được giữ cho build; 0 = không giữ.</summary>

        public static int BuilderInstanceId { get; private set; }



        public static void AssignBuilder(int workerInstanceId) => BuilderInstanceId = workerInstanceId;

        /// <summary>
        /// Mục tiêu: Gỡ worker khỏi slot build khi xây xong/hủy — economy có thể gán gather lại.
        /// Cách hoạt động: Clear BuilderInstanceId nếu trùng instanceId.
        /// </summary>
        public static void ReleaseBuilderIfMatches(int workerInstanceId)
        {
            if (workerInstanceId != 0 && BuilderInstanceId == workerInstanceId)
            {
                BuilderInstanceId = 0;
            }
        }

        /// <summary>

        /// Mục tiêu: Bỏ giữ builder khi worker không còn đi/xây (tránh gather chen vào lúc đang tới công trường).

        /// Cách hoạt động: Tìm worker theo BuilderInstanceId; clear nếu không <see cref="Worker.IsCommittedToConstructionWork"/>.

        /// </summary>

        public static void SyncReservedBuilder(IReadOnlyList<Worker> workers)

        {

            if (BuilderInstanceId == 0 || workers == null)

            {

                return;

            }



            for (int i = 0; i < workers.Count; i++)

            {

                Worker worker = workers[i];

                if (worker == null || worker.GetInstanceID() != BuilderInstanceId)

                {

                    continue;

                }



                if (!worker.IsCommittedToConstructionWork)
                {
                    BuilderInstanceId = 0;
                }

                return;

            }



            BuilderInstanceId = 0;

        }



        /// <summary>

        /// Mục tiêu: Economy/dispatcher không gán Gather cho worker đang build hoặc đang đi tới chỗ xây.

        /// Cách hoạt động: <see cref="Worker.IsCommittedToConstructionWork"/> hoặc trùng BuilderInstanceId đang giữ.

        /// </summary>

        public static bool ShouldSkipGatherForWorker(Worker worker)

        {

            if (worker == null)

            {

                return true;

            }



            if (worker.IsCommittedToConstructionWork)

            {

                return true;

            }



            return BuilderInstanceId != 0 && worker.GetInstanceID() == BuilderInstanceId;

        }



        public static bool ShouldSkipGatherForWorker(int workerInstanceId)

        {

            if (workerInstanceId == 0)

            {

                return false;

            }



            return BuilderInstanceId != 0 && workerInstanceId == BuilderInstanceId;

        }

    }

}



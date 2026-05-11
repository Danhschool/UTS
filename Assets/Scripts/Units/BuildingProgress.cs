using UnityEngine;
using UnityEngine.Serialization;

namespace GameDevTV.RTS.Units
{
    [System.Serializable]
    public struct BuildingProgress
    {
        public enum BuildingState
        {
            Building,
            Paused,
            Completed,
            Destroyed
        }
        [field: SerializeField] public float StartTime { get; private set; }
        [field: SerializeField, FormerlySerializedAs("<Progress>k__BackingField")]
        public float Completion { get; private set; }
        [field: SerializeField] public BuildingState State { get; private set; }

        public BuildingProgress(BuildingState state, float startTime, float completion)
        {
            State = state;
            StartTime = startTime;
            Completion = completion;
        }
    }
}
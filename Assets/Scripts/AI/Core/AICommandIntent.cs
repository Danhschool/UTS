using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// Một ý định lệnh trong hàng đợi ưu tiên — planner enqueue, dispatcher thực thi.
    /// </summary>
    public readonly struct AICommandIntent : System.IComparable<AICommandIntent>
    {
        public int Priority { get; }
        public int ManagerId { get; }
        public AbstractCommandable Entity { get; }
        public BaseCommand Command { get; }
        public RaycastHit Hit { get; }
        public int UnitIndex { get; }
        public MouseButton MouseButton { get; }

        public AICommandIntent(
            int priority,
            int managerId,
            AbstractCommandable entity,
            BaseCommand command,
            RaycastHit hit,
            int unitIndex = 0,
            MouseButton mouseButton = MouseButton.Right)
        {
            Priority = priority;
            ManagerId = managerId;
            Entity = entity;
            Command = command;
            Hit = hit;
            UnitIndex = unitIndex;
            MouseButton = mouseButton;
        }

        /// <summary>
        /// Mục tiêu: Sắp xếp hàng đợi — priority cao hơn được pop trước.
        /// Cách hoạt động: So sánh ngược Priority (descending).
        /// </summary>
        public int CompareTo(AICommandIntent other) => other.Priority.CompareTo(Priority);
    }

    /// <summary>Định danh manager cho logging và ưu tiên intent (M2+).</summary>
    public static class AIManagerIds
    {
        public const int Economy = 0;
        public const int Base = 1;
        public const int Military = 2;
    }
}

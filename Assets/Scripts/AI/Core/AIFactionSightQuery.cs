using System.Collections.Generic;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Tầm nhìn planner cho phe AI — union vòng SightRadius từ unit/building cùng owner (không dùng fog Player1).
    /// </summary>
    public static class AIFactionSightQuery
    {
        const float DefaultSightRadius = 12f;

        readonly struct SightObserver
        {
            public readonly Vector3 Position;
            public readonly float RadiusSqr;

            public SightObserver(Vector3 position, float radius)
            {
                Position = position;
                RadiusSqr = radius * radius;
            }
        }

        static readonly Dictionary<int, List<SightObserver>> ObserversByFaction = new(8);
        static readonly List<SightObserver> ScratchObservers = new(128);

        /// <summary>
        /// Mục tiêu: Cập nhật cache observer trước mỗi tick planner (tránh Find mỗi lần IsVisibleTo).
        /// Cách hoạt động: Gom Civil Central, buildings, units từ snapshot; lấy SightRadius từ SO.
        /// </summary>
        public static void RefreshFromSnapshot(AIWorldStateSnapshot snapshot)
        {
            if (snapshot == null || HumanFogVisionUtility.IsHumanPlayer(snapshot.Owner))
            {
                return;
            }

            int key = (int)snapshot.Owner;
            if (!ObserversByFaction.TryGetValue(key, out List<SightObserver> observers))
            {
                observers = new List<SightObserver>(64);
                ObserversByFaction[key] = observers;
            }

            observers.Clear();
            AddCommandableObserver(observers, snapshot.CivilCentral);

            for (int i = 0; i < snapshot.Buildings.Count; i++)
            {
                AddCommandableObserver(observers, snapshot.Buildings[i]);
            }

            for (int i = 0; i < snapshot.Units.Count; i++)
            {
                AddCommandableObserver(observers, snapshot.Units[i] as AbstractCommandable);
            }
        }

        public static void ClearFaction(Owner faction)
        {
            ObserversByFaction.Remove((int)faction);
        }

        /// <summary>
        /// Mục tiêu: Điểm world có nằm trong vòng nhìn bất kỳ unit/building phe AI không.
        /// Cách hoạt động: Duyệt cache tick hiện tại; fallback quét commandable owner nếu cache rỗng.
        /// </summary>
        public static bool IsWorldVisibleTo(Owner viewer, Vector3 worldPosition)
        {
            if (HumanFogVisionUtility.IsHumanPlayer(viewer))
            {
                return FactionFogQuery.IsWorldVisibleTo(viewer, worldPosition);
            }

            if (TryIsVisibleInCachedObservers(viewer, worldPosition))
            {
                return true;
            }

            return IsWorldVisibleToFallback(viewer, worldPosition);
        }

        /// <summary>
        /// Mục tiêu: API thống nhất cho <see cref="FactionFogQuery"/> và planner AI.
        /// </summary>
        public static bool IsVisibleTo(Owner viewer, IHideable hideable)
        {
            if (hideable == null)
            {
                return false;
            }

            if (hideable is AbstractCommandable commandable && commandable.Owner == viewer)
            {
                return true;
            }

            if (HumanFogVisionUtility.IsHumanPlayer(viewer))
            {
                return FactionFogQuery.IsVisibleTo(viewer, hideable);
            }

            return hideable.Transform != null
                   && IsWorldVisibleTo(viewer, hideable.Transform.position);
        }

        static bool TryIsVisibleInCachedObservers(Owner viewer, Vector3 worldPosition)
        {
            if (!ObserversByFaction.TryGetValue((int)viewer, out List<SightObserver> observers)
                || observers.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < observers.Count; i++)
            {
                SightObserver observer = observers[i];
                if ((worldPosition - observer.Position).sqrMagnitude <= observer.RadiusSqr)
                {
                    return true;
                }
            }

            return false;
        }

        static void AddCommandableObserver(List<SightObserver> observers, AbstractCommandable commandable)
        {
            if (commandable == null || commandable.CurrentHealth <= 0)
            {
                return;
            }

            float radius = commandable.UnitSO?.SightConfig != null
                ? commandable.UnitSO.SightConfig.SightRadius
                : DefaultSightRadius;
            observers.Add(new SightObserver(commandable.transform.position, radius));
        }

        /// <summary>
        /// Mục tiêu: Gọi IsVisibleTo ngoài tick AI (hiếm) vẫn đúng phe.
        /// Cách hoạt động: FindObjectsByType AbstractCommandable — chỉ khi cache trống.
        /// </summary>
        static bool IsWorldVisibleToFallback(Owner viewer, Vector3 worldPosition)
        {
            AISceneEntityCache.EnsureFresh();

            BaseBuilding[] buildings = AISceneEntityCache.Buildings;
            for (int i = 0; i < buildings.Length; i++)
            {
                if (IsWorldPositionVisibleToCommandable(viewer, worldPosition, buildings[i]))
                {
                    return true;
                }
            }

            AbstractUnit[] units = AISceneEntityCache.Units;
            for (int i = 0; i < units.Length; i++)
            {
                if (IsWorldPositionVisibleToCommandable(viewer, worldPosition, units[i]))
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsWorldPositionVisibleToCommandable(
            Owner viewer,
            Vector3 worldPosition,
            AbstractCommandable commandable)
        {
            if (commandable == null || commandable.Owner != viewer || commandable.CurrentHealth <= 0)
            {
                return false;
            }

            float radius = commandable.UnitSO?.SightConfig != null
                ? commandable.UnitSO.SightConfig.SightRadius
                : DefaultSightRadius;
            return (worldPosition - commandable.transform.position).sqrMagnitude <= radius * radius;
        }
    }
}

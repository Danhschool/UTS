using System.Collections.Generic;
using GameDevTV.RTS.Units;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Registry Owner human → <see cref="IFogMapQuery"/> (không Find mỗi frame).
    /// </summary>
    public static class FactionFogSystemsRegistry
    {
        static readonly Dictionary<Owner, IFogMapQuery> Queries = new();

        public static void Register(IFogMapQuery query)
        {
            if (query == null || !HumanFogVisionUtility.EmitsFogVision(query.FactionOwner))
            {
                return;
            }

            Queries[query.FactionOwner] = query;
        }

        public static void Unregister(IFogMapQuery query)
        {
            if (query == null)
            {
                return;
            }

            if (Queries.TryGetValue(query.FactionOwner, out IFogMapQuery existing) && ReferenceEquals(existing, query))
            {
                Queries.Remove(query.FactionOwner);
            }
        }

        public static bool TryGet(Owner owner, out IFogMapQuery query) =>
            Queries.TryGetValue(owner, out query);

        /// <summary>
        /// Mục tiêu: AI planner offline vẫn dùng fog Player1; human viewer map đúng faction.
        /// Cách hoạt động: Human → owner; AI/other → Player1 khi có registry P1.
        /// </summary>
        public static Owner ResolveFogViewer(Owner viewer)
        {
            if (HumanFogVisionUtility.EmitsFogVision(viewer))
            {
                return viewer;
            }

            return Owner.Player1;
        }
    }
}

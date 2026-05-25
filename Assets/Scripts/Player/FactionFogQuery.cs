using GameDevTV.RTS.AI;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: API tầm nhìn cho AI và gameplay — map viewer → fog faction tương ứng.
    /// </summary>
    public static class FactionFogQuery
    {
        /// <summary>
        /// Mục tiêu: Entity có trong tầm nhìn fog của viewer (ẩn/hiện model trên map).
        /// Cách hoạt động: Đọc <see cref="IHideable.IsVisible"/> sau <see cref="FactionVisibilityUpdater"/>.
        /// </summary>
        public static bool IsVisibleTo(Owner viewer, IHideable hideable)
        {
            if (hideable == null)
            {
                return false;
            }

            if (hideable is AbstractCommandable commandable
                && commandable.Owner == viewer)
            {
                return true;
            }

            if (HumanFogVisionUtility.IsHumanPlayer(viewer))
            {
                return hideable.IsVisible;
            }

            return AIFactionSightQuery.IsVisibleTo(viewer, hideable);
        }

        /// <summary>
        /// Mục tiêu: Điểm world đang trong vision RT của viewer (hiếm dùng).
        /// Cách hoạt động: Sample registry fog theo <see cref="FactionFogSystemsRegistry.ResolveFogViewer"/>.
        /// </summary>
        public static bool IsWorldVisibleTo(Owner viewer, Vector3 worldPosition)
        {
            if (HumanFogVisionUtility.IsHumanPlayer(viewer))
            {
                if (!FactionFogSystemsRegistry.TryGet(viewer, out IFogMapQuery query))
                {
                    return true;
                }

                return query.IsWorldVisible(worldPosition);
            }

            return AIFactionSightQuery.IsWorldVisibleTo(viewer, worldPosition);
        }

        /// <summary>
        /// Mục tiêu: Điểm world đã explored trên minimap / AI scout.
        /// Cách hoạt động: Sample explored RT qua <see cref="IFogMapQuery"/> của viewer.
        /// </summary>
        public static bool IsWorldExploredFor(Owner viewer, Vector3 worldPosition)
        {
            if (HumanFogVisionUtility.IsHumanPlayer(viewer))
            {
                if (!FactionFogSystemsRegistry.TryGet(viewer, out IFogMapQuery query))
                {
                    return false;
                }

                return query.IsWorldExplored(worldPosition);
            }

            return AIFactionSightQuery.IsWorldVisibleTo(viewer, worldPosition);
        }
    }
}

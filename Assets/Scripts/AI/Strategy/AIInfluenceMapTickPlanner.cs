using System.Collections.Generic;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Rebuild <see cref="AIInfluenceMap"/> một lần mỗi tick từ <see cref="AIWorldStateSnapshot"/>.
    /// </summary>
    public static class AIInfluenceMapTickPlanner
    {
        /// <summary>
        /// Mục tiêu: Chuẩn bị influence map dùng chung cho base/economy/military trong tick.
        /// Cách hoạt động: Thu threat + cụm mỏ xa → resolve config → Rebuild trên map instance.
        /// </summary>
        public static AIInfluenceMapTickContext Build(
            AIWorldStateSnapshot snapshot,
            AIBaseRuntimeConfig baseConfig,
            float remoteClusterMinDistance,
            AIInfluenceMap map,
            List<Vector3> threatScratch,
            List<Vector3> economicScratch)
        {
            if (snapshot?.CivilCentral == null || map == null)
            {
                return AIInfluenceMapTickContext.Empty;
            }

            Vector3 ccPosition = snapshot.CivilCentral.transform.position;
            AIInfluenceMapRuntimeConfig config = AIInfluenceMapConfigResolver.Resolve(snapshot, baseConfig);

            AIInfluenceMap.CollectThreatPositionsFromSnapshot(
                snapshot,
                snapshot.Owner,
                config.MapRadius,
                requireVisible: false,
                threatScratch);

            AIInfluenceMap.CollectRemoteEconomicPositionsFromSnapshot(
                snapshot,
                ccPosition,
                remoteClusterMinDistance,
                economicScratch);

            map.Rebuild(ccPosition, threatScratch, economicScratch, config);
            return new AIInfluenceMapTickContext(map, config, isValid: true);
        }
    }
}

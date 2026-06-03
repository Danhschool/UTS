using GameDevTV.RTS.Environment;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Tìm mỏ / target combat gần điểm click cho ClientRpc mirror (supply thường không có NetworkIdentity).
    /// </summary>
    public static class RtsUtsCommandMirrorUtility
    {
        const float GatherSearchRadius = 6f;

        /// <summary>
        /// Mục tiêu: ClientRpc mirror gather — tìm GatherableSupply gần vị trí server đã xác nhận.
        /// </summary>
        public static bool TryFindGatherableSupplyNear(Vector3 worldPoint, out GatherableSupply supply)
        {
            supply = null;
            Collider[] overlaps = Physics.OverlapSphere(
                worldPoint,
                GatherSearchRadius,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider collider = overlaps[i];
                if (collider == null)
                {
                    continue;
                }

                GatherableSupply candidate = collider.GetComponentInParent<GatherableSupply>();
                if (candidate == null || candidate.Amount <= 0)
                {
                    continue;
                }

                float distanceSq = (candidate.transform.position - worldPoint).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                supply = candidate;
            }

            return supply != null;
        }
    }
}

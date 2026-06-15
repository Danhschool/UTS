using GameDevTV.RTS.Buildings;
using GameDevTV.RTS.Units;
using Mirror;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Tắt simulation gameplay trên client thuần — chỉ server/host chạy BT, NavMesh, tháp bắn.
    /// </summary>
    public static class RtsNetplaySimulationGate
    {
        public static void ApplyToSpawnedEntity(GameObject instance)
        {
            if (instance == null || !RtsNetplaySession.IsNetworkMatch)
            {
                return;
            }

            if (RtsNetplaySession.IsPureClient)
            {
                DisableClientAuthoritativeComponents(instance);
            }
            else if (NetworkServer.active)
            {
                EnableServerAuthoritativeComponents(instance);
            }
        }

        static void DisableClientAuthoritativeComponents(GameObject root)
        {
            if (root.TryGetComponent(out BehaviorGraphAgent graphAgent))
            {
                graphAgent.enabled = false;
            }

            if (root.TryGetComponent(out NavMeshAgent agent))
            {
                agent.enabled = false;
            }

            BuildingAutoAttack[] towerAttacks = root.GetComponentsInChildren<BuildingAutoAttack>(true);
            for (int i = 0; i < towerAttacks.Length; i++)
            {
                if (towerAttacks[i] != null)
                {
                    towerAttacks[i].enabled = false;
                }
            }
        }

        static void EnableServerAuthoritativeComponents(GameObject root)
        {
            if (root.TryGetComponent(out BehaviorGraphAgent graphAgent))
            {
                graphAgent.enabled = true;
            }

            if (root.TryGetComponent(out NavMeshAgent agent))
            {
                agent.enabled = true;
            }
        }
    }
}

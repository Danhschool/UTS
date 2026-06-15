using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Gameplay;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Server relay UpgradeResearched / BuildingSpawn tech state xuống client MP.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class RtsUtsTechStateRelay : NetworkBehaviour
    {
        public static RtsUtsTechStateRelay Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[RtsUtsTechStateRelay] Trùng instance — giữ object đầu tiên.");
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            RegisterBusHandlers();
        }

        public override void OnStopServer()
        {
            UnregisterBusHandlers();
            base.OnStopServer();
        }

        void RegisterBusHandlers()
        {
            Bus<UpgradeResearchedEvent>.RegisterForAll(HandleUpgradeResearchedOnServer);
            Bus<BuildingSpawnEvent>.RegisterForAll(HandleBuildingSpawnOnServer);
            Bus<BuildingDeathEvent>.RegisterForAll(HandleBuildingDeathOnServer);
        }

        void UnregisterBusHandlers()
        {
            Bus<UpgradeResearchedEvent>.UnregisterForAll(HandleUpgradeResearchedOnServer);
            Bus<BuildingSpawnEvent>.UnregisterForAll(HandleBuildingSpawnOnServer);
            Bus<BuildingDeathEvent>.UnregisterForAll(HandleBuildingDeathOnServer);
        }

        public static void EnsureServerInstance()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            if (Instance != null)
            {
                return;
            }

            RtsUtsTechStateRelay existing = Object.FindFirstObjectByType<RtsUtsTechStateRelay>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                return;
            }

            GameplayMapCoreMarker core = Object.FindFirstObjectByType<GameplayMapCoreMarker>(
                FindObjectsInactive.Include);
            GameObject host = core != null ? core.gameObject : new GameObject(nameof(RtsUtsTechStateRelay));

            if (host.GetComponent<NetworkIdentity>() == null)
            {
                host.AddComponent<NetworkIdentity>();
            }

            if (host.GetComponent<RtsUtsTechStateRelay>() == null)
            {
                host.AddComponent<RtsUtsTechStateRelay>();
            }

            if (host.GetComponent<NetworkIdentity>().netId == 0)
            {
                NetworkServer.Spawn(host);
            }
        }

        void HandleUpgradeResearchedOnServer(UpgradeResearchedEvent evt)
        {
            if (!isServer || evt.Upgrade == null)
            {
                return;
            }

            RpcApplyUpgradeResearched(evt.Owner, evt.Upgrade.name);
        }

        void HandleBuildingSpawnOnServer(BuildingSpawnEvent evt)
        {
            if (!isServer || evt.Building == null || evt.Building.BuildingSO == null)
            {
                return;
            }

            RpcApplyBuildingSpawn(evt.Owner, evt.Building.BuildingSO.name);
        }

        void HandleBuildingDeathOnServer(BuildingDeathEvent evt)
        {
            if (!isServer || evt.Building == null || evt.Building.BuildingSO == null)
            {
                return;
            }

            RpcApplyBuildingDeath(evt.Owner, evt.Building.BuildingSO.name);
        }

        [ClientRpc]
        void RpcApplyUpgradeResearched(Owner owner, string upgradeAssetName)
        {
            if (isServer
                || !RtsUnlockableAssetCatalog.TryResolveUnlockable(upgradeAssetName, out UnlockableSO unlockable)
                || unlockable is not UpgradeSO upgrade)
            {
                return;
            }

            upgrade.TechTree.ApplyNetworkResearchCompleted(owner, upgrade);
            Bus<UpgradeResearchedEvent>.Raise(owner, new UpgradeResearchedEvent(owner, upgrade));
        }

        [ClientRpc]
        void RpcApplyBuildingDeath(Owner owner, string buildingAssetName)
        {
            if (isServer
                || !RtsUnlockableAssetCatalog.TryResolveBuilding(buildingAssetName, out BuildingSO buildingSo))
            {
                return;
            }

            buildingSo.TechTree.ApplyNetworkDependencyLost(owner, buildingSo);
        }

        [ClientRpc]
        void RpcApplyBuildingSpawn(Owner owner, string buildingAssetName)
        {
            if (isServer || !RtsUnlockableAssetCatalog.TryResolveBuilding(buildingAssetName, out BuildingSO buildingSo))
            {
                return;
            }

            buildingSo.TechTree.ApplyNetworkDependencyUnlocked(owner, buildingSo);
        }
    }
}

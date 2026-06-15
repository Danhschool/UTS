using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Server thực thi lệnh build/train/research sau khi client gửi Command.
    /// </summary>
    public static class RtsUtsGameplayCommandServer
    {
        public static bool TryExecuteBuild(
            uint workerNetId,
            string buildingAssetName,
            Vector3 worldPoint,
            uint resumeBuildingNetId,
            NetworkConnectionToClient connection)
        {
            if (!TryResolveUnit(workerNetId, connection, out Worker worker, out RtsUtsNetworkEntity workerEntity))
            {
                return false;
            }

            if (!RtsUnlockableAssetCatalog.TryResolveBuilding(buildingAssetName, out BuildingSO buildingSo))
            {
                return false;
            }

            Owner owner = workerEntity.UtsOwner;

            if (resumeBuildingNetId != 0
                && NetworkServer.spawned.TryGetValue(resumeBuildingNetId, out NetworkIdentity resumeIdentity)
                && resumeIdentity.TryGetComponent(out BaseBuilding resumeBuilding)
                && resumeBuilding.BuildingSO == buildingSo
                && resumeBuilding.CanResumeConstruction()
                && resumeIdentity.TryGetComponent(out RtsUtsNetworkEntity resumeEntity)
                && resumeEntity.UtsOwner == owner)
            {
                worker.ResumeBuilding(resumeBuilding);
                return true;
            }

            if (worker.IsBuilding || !SupplyAffordability.HasEnough(owner, buildingSo.Cost))
            {
                return false;
            }

            worker.Build(buildingSo, worldPoint);
            return true;
        }

        public static bool TryExecuteEnqueueUnlockable(
            uint buildingNetId,
            string unlockableAssetName,
            NetworkConnectionToClient connection)
        {
            if (!NetworkServer.spawned.TryGetValue(buildingNetId, out NetworkIdentity identity)
                || !identity.TryGetComponent(out BaseBuilding building)
                || !identity.TryGetComponent(out RtsUtsNetworkEntity networkEntity)
                || !networkEntity.ServerCanAcceptOrdersFrom(connection.connectionId))
            {
                return false;
            }

            if (!RtsUnlockableAssetCatalog.TryResolveUnlockable(unlockableAssetName, out UnlockableSO unlockable))
            {
                return false;
            }

            Owner owner = networkEntity.UtsOwner;

            if (!SupplyAffordability.HasEnough(owner, unlockable.Cost))
            {
                return false;
            }

            if (unlockable is UpgradeSO upgradeSo)
            {
                if (!building.CanEnqueueUpgrade(upgradeSo)
                    || !upgradeSo.TechTree.IsUnlocked(owner, upgradeSo)
                    || (upgradeSo.IsOneTimeUnlock && upgradeSo.TechTree.IsResearched(owner, upgradeSo)))
                {
                    return false;
                }
            }
            else if (unlockable is AbstractUnitSO unitSo)
            {
                if (!unitSo.TechTree.IsUnlocked(owner, unitSo))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            building.BuildUnlockable(unlockable);
            return true;
        }

        static bool TryResolveUnit(
            uint netId,
            NetworkConnectionToClient connection,
            out Worker worker,
            out RtsUtsNetworkEntity networkEntity)
        {
            worker = null;
            networkEntity = null;

            if (!NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity identity)
                || !identity.TryGetComponent(out networkEntity)
                || !networkEntity.ServerCanAcceptOrdersFrom(connection.connectionId)
                || !identity.TryGetComponent(out worker))
            {
                return false;
            }

            return true;
        }
    }
}

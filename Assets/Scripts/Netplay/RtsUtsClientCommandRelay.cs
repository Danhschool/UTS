using System.Collections.Generic;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Units.Formation;
using Mirror;
using ProjectRTS.Netplay;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Client relay lệnh UTS qua Command thay vì gọi Handle trực tiếp khi không phải host.
    /// </summary>
    public static class RtsUtsClientCommandRelay
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterBridge()
        {
            PlayerInputNetworkBridge.IsMultiplayerClient = () =>
                NetworkClient.active && NetworkClient.isConnected && !NetworkServer.active;

            PlayerInputNetworkBridge.TryRelayUnitCommand = TryRelay;
            PlayerInputNetworkBridge.TryRelayUnitStop = TryRelayStop;
            PlayerInputNetworkBridge.TryRelayActivateCommand = TryRelayActivate;
            PlayerInputNetworkBridge.TryRelayFormationMove = TryRelayFormationMove;
        }

        /// <summary>
        /// Mục tiêu: Move nhóm trên pure client — relay từng unit với đích formation lên server.
        /// Cách hoạt động: Tính lưới vuông giống <see cref="GroupFormationMoveUtility"/> rồi gọi RequestUtsMove từng netId.
        /// </summary>
        static bool TryRelayFormationMove(
            IReadOnlyList<AbstractUnit> units,
            RaycastHit hit,
            MoveCommand moveCommand)
        {
            if (units == null || units.Count <= 1 || moveCommand == null)
            {
                return false;
            }

            RtsUtsPlayerCommands commands = ResolveLocalCommands();
            if (commands == null)
            {
                return false;
            }

            float spacingMultiplier = moveCommand.FormationSpacingMultiplier;
            Vector3[] positions = UnitSquareFormationPlanner.ComputeWorldPositions(
                units,
                hit.point,
                spacingMultiplier);

            bool anyRelayed = false;
            for (int i = 0; i < units.Count && i < positions.Length; i++)
            {
                AbstractUnit unit = units[i];
                if (unit == null)
                {
                    continue;
                }

                if (!unit.TryGetComponent(out NetworkIdentity identity))
                {
                    continue;
                }

                if (!unit.TryGetComponent(out RtsUtsNetworkEntity networkEntity)
                    || !networkEntity.IsCommandableByLocalHuman)
                {
                    continue;
                }

                commands.RequestUtsMove(identity.netId, positions[i], i);
                anyRelayed = true;
            }

            return anyRelayed;
        }

        static bool TryRelay(
            AbstractUnit unit,
            RaycastHit hit,
            BaseCommand command,
            MouseButton mouseButton,
            int unitIndex)
        {
            if (unit == null || command == null)
            {
                return false;
            }

            if (!unit.TryGetComponent(out NetworkIdentity identity))
            {
                return false;
            }

            if (!unit.TryGetComponent(out RtsUtsNetworkEntity networkEntity)
                || !networkEntity.IsCommandableByLocalHuman)
            {
                return false;
            }

            RtsUtsPlayerCommands commands = ResolveLocalCommands();
            if (commands == null)
            {
                return false;
            }

            if (command is CancelBuildingCommand && unit is Worker)
            {
                commands.RequestUtsCancelWorkerBuild(identity.netId);
                return true;
            }

            if (command is GatherCommand)
            {
                Vector3 gatherPoint = hit.point;
                GatherableSupply supply = hit.collider != null
                    ? hit.collider.GetComponentInParent<GatherableSupply>()
                    : null;
                if (supply != null)
                {
                    gatherPoint = supply.transform.position;
                }

                commands.RequestUtsGather(identity.netId, gatherPoint, unitIndex);
                return true;
            }

            uint enemyTargetNetId = TryResolveEnemyTargetNetId(hit, unit.Owner);
            if (mouseButton == MouseButton.Right
                && unit is IAttacker
                && enemyTargetNetId != 0)
            {
                commands.RequestUtsAttack(identity.netId, hit.point, enemyTargetNetId, unitIndex);
                return true;
            }

            if (command is MoveCommand)
            {
                commands.RequestUtsMove(identity.netId, hit.point, unitIndex);
                return true;
            }

            if (command is AttackCommand)
            {
                uint targetNetId = TryResolveEnemyTargetNetId(hit, unit.Owner);
                commands.RequestUtsAttack(identity.netId, hit.point, targetNetId, unitIndex);
                return true;
            }

            return false;
        }

        static bool TryRelayStop(AbstractUnit unit)
        {
            if (unit == null)
            {
                return false;
            }

            if (!unit.TryGetComponent(out NetworkIdentity identity))
            {
                return false;
            }

            if (!unit.TryGetComponent(out RtsUtsNetworkEntity networkEntity)
                || !networkEntity.IsCommandableByLocalHuman)
            {
                return false;
            }

            RtsUtsPlayerCommands commands = ResolveLocalCommands();
            if (commands == null)
            {
                return false;
            }

            commands.RequestUtsStop(identity.netId);
            return true;
        }

        static bool TryRelayActivate(
            BaseCommand command,
            IReadOnlyList<AbstractCommandable> commandables,
            RaycastHit hit)
        {
            if (command == null || commandables == null || commandables.Count == 0)
            {
                return false;
            }

            RtsUtsPlayerCommands commands = ResolveLocalCommands();
            if (commands == null)
            {
                return false;
            }

            if (command is CancelBuildingCommand)
            {
                for (int i = 0; i < commandables.Count; i++)
                {
                    if (commandables[i] is Worker worker
                        && worker.TryGetComponent(out NetworkIdentity workerIdentity)
                        && worker.TryGetComponent(out RtsUtsNetworkEntity workerEntity)
                        && workerEntity.IsCommandableByLocalHuman)
                    {
                        commands.RequestUtsCancelWorkerBuild(workerIdentity.netId);
                        return true;
                    }
                }

                return false;
            }

            if (command is BuildBuildingCommand buildCommand && buildCommand.Building != null)
            {
                uint resumeNetId = 0;
                if (hit.collider != null)
                {
                    BaseBuilding resumeTarget = hit.collider.GetComponentInParent<BaseBuilding>();
                    if (resumeTarget != null
                        && resumeTarget.TryGetComponent(out NetworkIdentity resumeIdentity)
                        && resumeTarget.BuildingSO == buildCommand.Building
                        && resumeTarget.CanResumeConstruction())
                    {
                        resumeNetId = resumeIdentity.netId;
                    }
                }

                for (int i = 0; i < commandables.Count; i++)
                {
                    if (commandables[i] is not Worker worker
                        || !worker.TryGetComponent(out NetworkIdentity workerIdentity)
                        || !worker.TryGetComponent(out RtsUtsNetworkEntity workerEntity)
                        || !workerEntity.IsCommandableByLocalHuman)
                    {
                        continue;
                    }

                    commands.RequestUtsBuild(
                        workerIdentity.netId,
                        buildCommand.Building.name,
                        hit.point,
                        resumeNetId);
                    return true;
                }

                return false;
            }

            if (command is BuildUnitCommand unitCommand && unitCommand.Unit != null)
            {
                return TryRelayEnqueueOnBuilding(commandables, unitCommand.Unit.name, commands);
            }

            if (command is ResearchUpgradeCommand researchCommand && researchCommand.Upgrade != null)
            {
                return TryRelayEnqueueOnBuilding(commandables, researchCommand.Upgrade.name, commands);
            }

            return false;
        }

        static bool TryRelayEnqueueOnBuilding(
            IReadOnlyList<AbstractCommandable> commandables,
            string unlockableAssetName,
            RtsUtsPlayerCommands commands)
        {
            for (int i = 0; i < commandables.Count; i++)
            {
                if (commandables[i] is not BaseBuilding building
                    || !building.TryGetComponent(out NetworkIdentity buildingIdentity)
                    || !building.TryGetComponent(out RtsUtsNetworkEntity buildingEntity)
                    || !buildingEntity.IsCommandableByLocalHuman)
                {
                    continue;
                }

                commands.RequestUtsEnqueueUnlockable(buildingIdentity.netId, unlockableAssetName);
                return true;
            }

            return false;
        }

        static RtsUtsPlayerCommands ResolveLocalCommands()
        {
            if (NetworkClient.localPlayer != null
                && NetworkClient.localPlayer.TryGetComponent(out RtsUtsPlayerCommands localCommands))
            {
                return localCommands;
            }

            foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
            {
                if (identity == null || !identity.isLocalPlayer)
                {
                    continue;
                }

                if (identity.TryGetComponent(out RtsUtsPlayerCommands commands))
                {
                    return commands;
                }
            }

            return null;
        }

        /// <summary>
        /// Mục tiêu: Gửi netId địch lên server — tránh TryBuildCombatHit fail do lệch vị trí / collider host.
        /// Cách hoạt động: Lấy NetworkIdentity trên collider IDamageable khác owner unit đang ra lệnh.
        /// </summary>
        static uint TryResolveEnemyTargetNetId(RaycastHit hit, Owner friendlyOwner)
        {
            if (hit.collider == null)
            {
                return 0;
            }

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable == null)
            {
                return 0;
            }

            Owner targetOwner = Owner.Invalid;
            if (damageable is AbstractCommandable commandable)
            {
                targetOwner = LocalCommandableOwnership.ResolveOwner(commandable);
            }
            else
            {
                targetOwner = damageable.Owner;
            }

            if (targetOwner == friendlyOwner || targetOwner == Owner.Invalid)
            {
                return 0;
            }

            NetworkIdentity targetIdentity = hit.collider.GetComponentInParent<NetworkIdentity>();
            return targetIdentity != null ? targetIdentity.netId : 0;
        }
    }
}

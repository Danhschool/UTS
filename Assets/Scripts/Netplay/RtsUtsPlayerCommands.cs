using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Commands Mirror cho unit/building UTS (game assembly, gắn cùng player prefab với RtsGameCommander).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RtsUtsPlayerCommands : NetworkBehaviour
    {
        [Command]
        void CmdUtsMoveUnit(uint netId, Vector3 destination, int formationIndex)
        {
            if (!TryResolveCommandable(netId, out RtsUtsNetworkEntity networkEntity, out AbstractUnit unit))
            {
                return;
            }

            MoveCommand moveCommand = ScriptableObject.CreateInstance<MoveCommand>();
            CommandContext context = new CommandContext(
                networkEntity.UtsOwner,
                unit,
                new RaycastHit { point = destination },
                formationIndex);

            if (moveCommand.CanHandle(context))
            {
                moveCommand.Handle(context);
                networkEntity?.RpcMirrorMoveGoal(destination);
            }

            Destroy(moveCommand);
        }

        [Command]
        void CmdUtsStop(uint netId)
        {
            if (!TryResolveCommandable(netId, out RtsUtsNetworkEntity networkEntity, out AbstractUnit unit))
            {
                return;
            }

            unit.Stop();
            networkEntity?.RpcMirrorStop();
        }

        [Command]
        void CmdUtsCancelWorkerBuild(uint workerNetId)
        {
            if (!TryResolveCommandable(workerNetId, out _, out AbstractUnit unit)
                || unit is not Worker worker)
            {
                return;
            }

            worker.CancelBuilding();
        }

        [Command]
        void CmdUtsGather(uint netId, Vector3 hitPoint, int formationIndex)
        {
            if (!TryResolveCommandable(netId, out RtsUtsNetworkEntity networkEntity, out AbstractUnit unit))
            {
                // #region agent log
                DebugSessionLog013c46.Write(
                    "P7",
                    "RtsUtsPlayerCommands.CmdUtsGather",
                    "reject resolve commandable",
                    $"{{\"netId\":{netId},\"conn\":{connectionToClient.connectionId}}}");
                // #endregion
                return;
            }

            if (!TryBuildGatherHit(hitPoint, out RaycastHit hit))
            {
                // #region agent log
                DebugSessionLog013c46.Write(
                    "P7",
                    "RtsUtsPlayerCommands.CmdUtsGather",
                    "reject gather hit",
                    $"{{\"netId\":{netId},\"owner\":\"{networkEntity.UtsOwner}\",\"conn\":{connectionToClient.connectionId}}}");
                // #endregion
                return;
            }

            // #region agent log
            DebugSessionLog013c46.Write(
                "P7",
                "RtsUtsPlayerCommands.CmdUtsGather",
                "accepted",
                $"{{\"netId\":{netId},\"owner\":\"{networkEntity.UtsOwner}\",\"conn\":{connectionToClient.connectionId}}}");
            // #endregion
            TryExecuteGatherCommand(networkEntity, unit, hit, formationIndex);
        }

        [Command]
        void CmdUtsAttack(uint netId, Vector3 hitPoint, uint targetNetId, int formationIndex)
        {
            if (!TryResolveCommandable(netId, out RtsUtsNetworkEntity networkEntity, out AbstractUnit unit))
            {
                return;
            }

            Owner attackerOwner = networkEntity.UtsOwner;

            if (targetNetId != 0
                && TryResolveCombatTargetByNetId(
                    targetNetId,
                    attackerOwner,
                    out IDamageable resolvedTarget,
                    out Collider resolvedCollider))
            {
                TryExecuteAttackOnDamageable(networkEntity, unit, resolvedTarget, resolvedCollider);
                return;
            }

            if (TryBuildCombatHit(hitPoint, attackerOwner, out RaycastHit hit))
            {
                TryExecuteAttackCommand(networkEntity, unit, hit, formationIndex);
                return;
            }

            if (unit is IAttacker attacker)
            {
                attacker.Attack(hitPoint);
            }
        }

        /// <summary>
        /// Mục tiêu: Server thử mọi GatherCommand SO trên unit — không return sớm khi CanHandle fail.
        /// </summary>
        static void TryExecuteGatherCommand(
            RtsUtsNetworkEntity networkEntity,
            AbstractUnit unit,
            RaycastHit hit,
            int formationIndex)
        {
            Owner owner = networkEntity != null ? networkEntity.UtsOwner : unit.Owner;

            if (unit is Worker worker
                && hit.collider != null
                && hit.collider.GetComponentInParent<GatherableSupply>() is GatherableSupply supply
                && supply.Amount > 0)
            {
                worker.Gather(supply);
                networkEntity?.RpcMirrorGatherPresentation(supply.transform.position);
                return;
            }

            CommandContext context = new CommandContext(owner, unit, hit, formationIndex, MouseButton.Right);

            foreach (BaseCommand candidate in AvailableCommandsResolver.GetFlattened(unit))
            {
                if (candidate is not GatherCommand gatherCommand)
                {
                    continue;
                }

                if (gatherCommand.CanHandle(context))
                {
                    gatherCommand.Handle(context);
                    return;
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Server thực thi attack cho client P2 — không phụ thuộc fog visibility trên host (chỉ P1 fog chạy).
        /// </summary>
        static void TryExecuteAttackCommand(
            RtsUtsNetworkEntity networkEntity,
            AbstractUnit unit,
            RaycastHit hit,
            int formationIndex)
        {
            Owner owner = networkEntity != null ? networkEntity.UtsOwner : unit.Owner;

            if (unit is not IAttacker attacker)
            {
                return;
            }

            if (hit.collider != null)
            {
                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null
                    && damageable.Owner != owner
                    && damageable.Owner != Owner.Invalid)
                {
                    attacker.Attack(damageable);
                    TryMirrorAttackTarget(networkEntity, hit.collider);
                    return;
                }
            }

            Vector3 attackPoint = hit.point;
            if (attackPoint == default && hit.collider != null)
            {
                attackPoint = hit.collider.bounds.center;
            }

            attacker.Attack(attackPoint);
        }

        static void TryMirrorAttackTarget(RtsUtsNetworkEntity networkEntity, Collider collider)
        {
            if (networkEntity == null || collider == null)
            {
                return;
            }

            NetworkIdentity targetIdentity = collider.GetComponentInParent<NetworkIdentity>();
            if (targetIdentity != null && targetIdentity.netId != 0)
            {
                networkEntity.RpcMirrorAttackTarget(targetIdentity.netId);
            }
        }

        /// <summary>
        /// Mục tiêu: Server attack địch đã resolve theo netId — không cần RaycastHit giả.
        /// Cách hoạt động: Gọi IAttacker.Attack(IDamageable) và mirror presentation qua collider.
        /// </summary>
        static void TryExecuteAttackOnDamageable(
            RtsUtsNetworkEntity networkEntity,
            AbstractUnit unit,
            IDamageable damageable,
            Collider collider)
        {
            if (unit is not IAttacker attacker || damageable == null)
            {
                return;
            }

            attacker.Attack(damageable);
            TryMirrorAttackTarget(networkEntity, collider);
        }

        /// <summary>
        /// Mục tiêu: Server resolve địch theo netId client gửi — không cần raycast physics trùng khớp host.
        /// Cách hoạt động: Tra NetworkServer.spawned, kiểm tra Owner địch, lấy IDamageable + Collider.
        /// </summary>
        static bool TryResolveCombatTargetByNetId(
            uint targetNetId,
            Owner attackerOwner,
            out IDamageable damageable,
            out Collider collider)
        {
            damageable = null;
            collider = null;

            if (!NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity identity))
            {
                return false;
            }

            damageable = identity.GetComponentInParent<IDamageable>();
            if (damageable == null
                || damageable.Owner == attackerOwner
                || damageable.Owner == Owner.Invalid)
            {
                return false;
            }

            collider = identity.GetComponentInChildren<Collider>();
            return true;
        }

        bool TryResolveCommandable(
            uint netId,
            out RtsUtsNetworkEntity networkEntity,
            out AbstractUnit unit)
        {
            networkEntity = null;
            unit = null;

            if (!NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity ni))
            {
                return false;
            }

            if (!ni.TryGetComponent(out networkEntity)
                || !networkEntity.ServerCanAcceptOrdersFrom(connectionToClient.connectionId))
            {
                return false;
            }

            return ni.TryGetComponent(out unit);
        }

        static bool TryBuildGatherHit(Vector3 worldPoint, out RaycastHit hit)
        {
            hit = default;
            const float searchRadius = 6f;
            Vector3 rayOrigin = worldPoint + Vector3.up * 80f;
            RaycastHit[] hits = Physics.RaycastAll(
                rayOrigin,
                Vector3.down,
                160f,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            float bestDistanceSq = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null)
                {
                    continue;
                }

                GatherableSupply supply = collider.GetComponentInParent<GatherableSupply>();
                if (supply == null || supply.Amount <= 0)
                {
                    continue;
                }

                float distanceSq = (supply.transform.position - worldPoint).sqrMagnitude;
                if (distanceSq > searchRadius * searchRadius || distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                hit = hits[i];
                found = true;
            }

            return found;
        }

        /// <summary>
        /// Mục tiêu: Server tìm collider địch gần điểm click (client MP không chạy Attack local).
        /// </summary>
        static bool TryBuildCombatHit(
            Vector3 worldPoint,
            Owner attackerOwner,
            out RaycastHit hit)
        {
            hit = default;
            const float searchRadius = 6f;
            Vector3 rayOrigin = worldPoint + Vector3.up * 80f;
            RaycastHit[] hits = Physics.RaycastAll(
                rayOrigin,
                Vector3.down,
                160f,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            float bestDistanceSq = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null)
                {
                    continue;
                }

                IDamageable damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null)
                {
                    continue;
                }

                if (damageable.Owner == attackerOwner || damageable.Owner == Owner.Invalid)
                {
                    continue;
                }

                float distanceSq = (collider.transform.position - worldPoint).sqrMagnitude;
                if (distanceSq > searchRadius * searchRadius || distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                hit = hits[i];
                found = true;
            }

            if (found)
            {
                return true;
            }

            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 160f)
                && hit.collider != null)
            {
                IDamageable fallbackTarget = hit.collider.GetComponentInParent<IDamageable>();
                return fallbackTarget != null
                    && fallbackTarget.Owner != attackerOwner
                    && fallbackTarget.Owner != Owner.Invalid;
            }

            return false;
        }

        public void RequestUtsStop(uint netId)
        {
            if (!isLocalPlayer)
            {
                return;
            }

            CmdUtsStop(netId);
        }

        public void RequestUtsCancelWorkerBuild(uint workerNetId)
        {
            if (!isLocalPlayer)
            {
                return;
            }

            CmdUtsCancelWorkerBuild(workerNetId);
        }

        public void RequestUtsMove(uint netId, Vector3 worldPoint, int formationIndex)
        {
            if (!isLocalPlayer)
            {
                return;
            }

            CmdUtsMoveUnit(netId, worldPoint, formationIndex);
        }

        public void RequestUtsGather(uint netId, Vector3 hitPoint, int formationIndex)
        {
            if (!isLocalPlayer)
            {
                return;
            }

            CmdUtsGather(netId, hitPoint, formationIndex);
        }

        public void RequestUtsAttack(uint netId, Vector3 hitPoint, uint targetNetId, int formationIndex)
        {
            if (!isLocalPlayer)
            {
                return;
            }

            CmdUtsAttack(netId, hitPoint, targetNetId, formationIndex);
        }

        public void RequestUtsBuild(uint workerNetId, string buildingAssetName, Vector3 worldPoint, uint resumeBuildingNetId)
        {
            if (!isLocalPlayer)
            {
                return;
            }

            CmdUtsBuildBuilding(workerNetId, buildingAssetName, worldPoint, resumeBuildingNetId);
        }

        public void RequestUtsEnqueueUnlockable(uint buildingNetId, string unlockableAssetName)
        {
            if (!isLocalPlayer)
            {
                return;
            }

            CmdUtsEnqueueUnlockable(buildingNetId, unlockableAssetName);
        }

        [Command]
        void CmdUtsBuildBuilding(uint workerNetId, string buildingAssetName, Vector3 worldPoint, uint resumeBuildingNetId)
        {
            bool executed = RtsUtsGameplayCommandServer.TryExecuteBuild(
                workerNetId,
                buildingAssetName,
                worldPoint,
                resumeBuildingNetId,
                connectionToClient);

            if (!NetworkServer.spawned.TryGetValue(workerNetId, out NetworkIdentity identity)
                || !identity.TryGetComponent(out RtsUtsNetworkEntity networkEntity))
            {
                return;
            }

            if (!executed)
            {
                // #region agent log
                DebugSessionLog013c46.Write(
                    "B1",
                    "RtsUtsPlayerCommands.CmdUtsBuildBuilding",
                    "build rejected",
                    $"{{\"workerNetId\":{workerNetId},\"building\":\"{buildingAssetName}\",\"conn\":{connectionToClient.connectionId}}}");
                // #endregion
                networkEntity.RpcNotifyBuildCommandRejected();
                return;
            }

            networkEntity.RpcMirrorBuildPresentation(buildingAssetName, worldPoint);
        }

        [Command]
        void CmdUtsEnqueueUnlockable(uint buildingNetId, string unlockableAssetName)
        {
            RtsUtsGameplayCommandServer.TryExecuteEnqueueUnlockable(
                buildingNetId,
                unlockableAssetName,
                connectionToClient);
        }
    }
}

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
            }

            Destroy(moveCommand);
        }

        [Command]
        void CmdUtsStop(uint netId)
        {
            if (!TryResolveCommandable(netId, out _, out AbstractUnit unit))
            {
                return;
            }

            unit.Stop();
        }

        [Command]
        void CmdUtsGather(uint netId, Vector3 hitPoint, int formationIndex)
        {
            if (!TryResolveCommandable(netId, out RtsUtsNetworkEntity networkEntity, out AbstractUnit unit))
            {
                return;
            }

            if (!TryBuildGatherHit(hitPoint, out RaycastHit hit))
            {
                return;
            }

            TryExecuteGatherCommand(networkEntity, unit, hit, formationIndex);
        }

        [Command]
        void CmdUtsAttack(uint netId, Vector3 hitPoint, int formationIndex)
        {
            if (!TryResolveCommandable(netId, out RtsUtsNetworkEntity networkEntity, out AbstractUnit unit))
            {
                return;
            }

            if (!TryBuildCombatHit(hitPoint, networkEntity.UtsOwner, out RaycastHit hit))
            {
                return;
            }

            TryExecuteAttackCommand(networkEntity, unit, hit, formationIndex);
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

            if (unit is IAttacker attacker
                && hit.collider != null)
            {
                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null
                    && damageable.Owner != owner
                    && damageable.Owner != Owner.Invalid)
                {
                    attacker.Attack(damageable);
                    NetworkIdentity targetIdentity = hit.collider.GetComponentInParent<NetworkIdentity>();
                    if (targetIdentity != null)
                    {
                        networkEntity?.RpcMirrorAttackTarget(targetIdentity.netId);
                    }

                    return;
                }
            }

            CommandContext context = new CommandContext(owner, unit, hit, formationIndex, MouseButton.Right);
            foreach (BaseCommand candidate in AvailableCommandsResolver.GetFlattened(unit))
            {
                if (candidate is AttackCommand attackCommand && attackCommand.CanHandle(context))
                {
                    attackCommand.Handle(context);
                    return;
                }
            }
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

        public void RequestUtsAttack(uint netId, Vector3 hitPoint, int formationIndex)
        {
            if (!isLocalPlayer)
            {
                return;
            }

            CmdUtsAttack(netId, hitPoint, formationIndex);
        }
    }
}

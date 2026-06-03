using System.Collections;

using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;



namespace GameDevTV.RTS.Netplay

{

    /// <summary>

    /// SRP: Đồng bộ <see cref="Owner"/> UTS trên unit/building networked; server là nguồn sự thật.

    /// </summary>

    [DisallowMultipleComponent]

    [RequireComponent(typeof(NetworkIdentity))]

    public sealed class RtsUtsNetworkEntity : NetworkBehaviour

    {

        [SyncVar(hook = nameof(HookUtsOwner))]

        Owner utsOwner = Owner.Invalid;



        [SyncVar]

        int serverOwnerConnectionId = -1;



        AbstractCommandable _commandable;



        public Owner UtsOwner => utsOwner;



        public bool IsCommandableByLocalHuman =>

            isClient

            && LocalHumanOwnerService.Instance != null

            && LocalHumanOwnerService.Instance.IsInitialized

            && LocalHumanOwnerService.Instance.IsLocalOwner(utsOwner);



        void Awake()

        {

            _commandable = GetComponent<AbstractCommandable>();

        }



        /// <summary>

        /// Mục tiêu: Server gán phe và connection sau spawn.

        /// Cách hoạt động: Set SyncVar; hook áp Owner + fog vision lên commandable.

        /// </summary>

        [Server]

        public void ServerConfigure(int connectionId, Owner owner)

        {

            serverOwnerConnectionId = connectionId;

            utsOwner = owner;

            ApplyOwnerToCommandable();

        }



        public override void OnStartClient()

        {

            base.OnStartClient();

            ApplyOwnerToCommandable();

            StartCoroutine(ApplyFogVisionLayersWhenReady());

        }



        /// <summary>

        /// Mục tiêu: Client P2 — Vision.prefab con spawn trễ / layer 14 mặc định cần gán lại vài frame.

        /// </summary>

        IEnumerator ApplyFogVisionLayersWhenReady()

        {

            for (int i = 0; i < 30; i++)

            {

                ApplyOwnerToCommandable();

                yield return null;

            }

        }



        void HookUtsOwner(Owner oldOwner, Owner newOwner) => ApplyOwnerToCommandable();



        void ApplyOwnerToCommandable()

        {

            if (_commandable == null || utsOwner == Owner.Invalid)

            {

                return;

            }



            _commandable.SyncOwnerAndFogVision(utsOwner);

        }



        public bool ServerCanAcceptOrdersFrom(int connectionId) =>

            connectionId == serverOwnerConnectionId;

        /// <summary>
        /// Mục tiêu: Client (P2) thấy worker gather sau server đã xử lý Command.
        /// Cách hoạt động: Tìm mỏ gần điểm hit, gọi <see cref="Worker.MirrorGatherPresentation"/>.
        /// </summary>
        [ClientRpc]
        public void RpcMirrorGatherPresentation(Vector3 supplyWorldPosition)
        {
            if (isServer || !TryGetComponent(out Worker worker))
            {
                return;
            }

            if (RtsUtsCommandMirrorUtility.TryFindGatherableSupplyNear(supplyWorldPosition, out GatherableSupply supply))
            {
                worker.MirrorGatherPresentation(supply);
            }
        }

        /// <summary>
        /// Mục tiêu: Client thấy unit attack đúng target sau server xử lý.
        /// </summary>
        [ClientRpc]
        public void RpcMirrorAttackTarget(uint targetNetId)
        {
            if (isServer || !TryGetComponent(out IAttacker attacker))
            {
                return;
            }

            if (!NetworkClient.spawned.TryGetValue(targetNetId, out NetworkIdentity targetIdentity))
            {
                return;
            }

            IDamageable damageable = targetIdentity.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                attacker.Attack(damageable);
            }
        }

    }

}



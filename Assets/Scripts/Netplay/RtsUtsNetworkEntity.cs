using System.Collections;

using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
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



        public bool IsCommandableByLocalHuman
        {
            get
            {
                if (!isClient || _commandable == null)
                {
                    return false;
                }

                return LocalCommandableOwnership.IsOwnedByLocalHuman(_commandable);
            }
        }

        /// <summary>Server debug: connection sở hữu entity (SyncVar).</summary>
        public int ServerOwnerConnectionId => serverOwnerConnectionId;



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

            RtsNetplaySimulationGate.ApplyToSpawnedEntity(gameObject);

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

            RaiseClientSpawnPresentationIfNeeded();

            if (isClient && !isServer && LocalCommandableOwnership.IsOwnedByLocalHuman(_commandable))
            {
                MpFogVisionSpawnRefresh.SchedulePresentationRetries();
            }
        }

        /// <summary>
        /// Mục tiêu: Client MP đăng ký unit/nhà spawn với minimap, fog, PlayerInput.
        /// Cách hoạt động: Gọi NotifySpawned / NotifyNetworkSpawnPresentation sau khi Owner sync.
        /// </summary>
        void RaiseClientSpawnPresentationIfNeeded()
        {
            if (isServer || _commandable == null)
            {
                return;
            }

            if (_commandable is AbstractUnit unit)
            {
                unit.NotifySpawned();
                return;
            }

            if (_commandable is BaseBuilding building)
            {
                building.NotifyNetworkSpawnPresentation();
            }
        }



        public bool ServerCanAcceptOrdersFrom(int connectionId) =>

            connectionId == serverOwnerConnectionId;

        /// <summary>
        /// Mục tiêu: Lấy connection sở hữu entity khi server spawn unit/building theo phe.
        /// Cách hoạt động: Tra serverOwnerConnectionId trong NetworkServer.connections.
        /// </summary>
        public NetworkConnectionToClient ResolveOwnerConnection()
        {
            if (serverOwnerConnectionId < 0)
            {
                return null;
            }

            return NetworkServer.connections.TryGetValue(serverOwnerConnectionId, out NetworkConnectionToClient connection)
                ? connection
                : null;
        }

        /// <summary>
        /// Mục tiêu: Client (P2) thấy worker gather sau server đã xử lý Command.
        /// Cách hoạt động: Tìm mỏ gần điểm hit, gọi <see cref="Worker.MirrorGatherPresentation"/>.
        /// </summary>
        [ClientRpc]
        public void RpcMirrorMoveGoal(Vector3 destination)
        {
            if (isServer || !TryGetComponent(out AbstractUnit unit))
            {
                return;
            }

            unit.MirrorMovePresentation(destination);
        }

        [ClientRpc]
        public void RpcMirrorStop()
        {
            if (isServer || !TryGetComponent(out AbstractUnit unit))
            {
                return;
            }

            unit.MirrorStopPresentation();
        }

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
        /// Mục tiêu: Client P2 mirror animation/di chuyển worker khi server nhận lệnh build.
        /// Cách hoạt động: Resolve BuildingSO theo tên asset rồi gọi Worker.MirrorBuildPresentation.
        /// </summary>
        [ClientRpc]
        public void RpcMirrorBuildPresentation(string buildingAssetName, Vector3 targetLocation)
        {
            if (isServer || !TryGetComponent(out Worker worker))
            {
                return;
            }

            if (!RtsUnlockableAssetCatalog.TryResolveBuildingForPresentation(buildingAssetName, out BuildingSO buildingSo))
            {
                return;
            }

            worker.MirrorBuildPresentation(buildingSo, targetLocation);
        }

        /// <summary>
        /// Mục tiêu: Client P2 gắn đúng nhà replicate từ server (tránh lệch vị trí Y khi nhà đang chôn).
        /// </summary>
        [ClientRpc]
        public void RpcLinkClientPresentationBuilding(
            uint buildingNetId,
            Vector3 targetLocation,
            float constructionCompletion,
            float buildTimeSeconds)
        {
            if (isServer || !TryGetComponent(out Worker worker))
            {
                return;
            }

            if (!NetworkClient.spawned.TryGetValue(buildingNetId, out NetworkIdentity identity)
                || !identity.TryGetComponent(out BaseBuilding building))
            {
                return;
            }

            building.SeedClientConstructionPresentationAnchor(
                targetLocation,
                constructionCompletion,
                buildTimeSeconds);
            worker.LinkPresentationBuildingUnderConstruction(building);

            if (identity.TryGetComponent(out RtsUtsNetworkBuildingSync buildingSync))
            {
                buildingSync.RefreshClientPresentationFromNetwork();
            }
        }

        /// <summary>
        /// Mục tiêu: Pure client nhận xác nhận nhà xây xong (backup khi SyncVar hook không kịp).
        /// </summary>
        [ClientRpc]
        public void RpcNotifyBuildingConstructionCompleted()
        {
            if (isServer)
            {
                return;
            }

            if (TryGetComponent(out RtsUtsNetworkBuildingSync buildingSync))
            {
                buildingSync.RefreshClientPresentationFromNetwork();
            }

            if (TryGetComponent(out BaseBuilding building))
            {
                building.ForceClientConstructionCompletedPresentation();
            }
        }

        /// <summary>
        /// Mục tiêu: Pure client worker thoát trạng thái build sau server CompleteConstruction.
        /// </summary>
        [ClientRpc]
        public void RpcNotifyWorkerBuildPresentationComplete()
        {
            if (isServer || !TryGetComponent(out Worker worker))
            {
                return;
            }

            worker.ReleasePlannerControlAfterConstructionEnded();
        }

        /// <summary>
        /// Mục tiêu: Pure client gỡ ghost đặt nhà khi server từ chối lệnh build.
        /// Cách hoạt động: Raise BuildingConstructStartedEvent — PlayerInput handler chỉ DisposePlacementGhost.
        /// </summary>
        [ClientRpc]
        public void RpcNotifyBuildCommandRejected()
        {
            if (isServer || !IsCommandableByLocalHuman)
            {
                return;
            }

            Bus<BuildingConstructStartedEvent>.Raise(UtsOwner, new BuildingConstructStartedEvent(UtsOwner));
        }

        /// <summary>
        /// Mục tiêu: Client thấy unit attack đúng target sau server xử lý.
        /// </summary>
        [ClientRpc]
        public void RpcMirrorAttackTarget(uint targetNetId)
        {
            if (isServer || targetNetId == 0)
            {
                return;
            }

            if (!NetworkClient.spawned.TryGetValue(targetNetId, out NetworkIdentity targetIdentity))
            {
                return;
            }

            IDamageable damageable = targetIdentity.GetComponentInParent<IDamageable>();
            if (damageable == null)
            {
                return;
            }

            if (TryGetComponent(out AbstractUnit unit))
            {
                unit.MirrorAttackPresentation(damageable);
                return;
            }

            if (TryGetComponent(out IAttacker attacker))
            {
                attacker.Attack(damageable);
            }
        }

    }

}



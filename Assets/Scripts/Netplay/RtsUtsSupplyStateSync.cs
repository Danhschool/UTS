using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Game;
using GameDevTV.RTS.Gameplay;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Server-authoritative đồng bộ tài nguyên/dân cho MP (Player1 + Player2).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class RtsUtsSupplyStateSync : NetworkBehaviour
    {
        public static RtsUtsSupplyStateSync Instance { get; private set; }

        [SyncVar(hook = nameof(HookP1Stone))] int syncP1Stone = 1000;
        [SyncVar(hook = nameof(HookP1Wood))] int syncP1Wood = 1000;
        [SyncVar(hook = nameof(HookP1Food))] int syncP1Food = 1000;
        [SyncVar(hook = nameof(HookP1Population))] int syncP1Population;
        [SyncVar(hook = nameof(HookP1PopulationLimit))] int syncP1PopulationLimit;

        [SyncVar(hook = nameof(HookP2Stone))] int syncP2Stone = 1000;
        [SyncVar(hook = nameof(HookP2Wood))] int syncP2Wood = 1000;
        [SyncVar(hook = nameof(HookP2Food))] int syncP2Food = 1000;
        [SyncVar(hook = nameof(HookP2Population))] int syncP2Population;
        [SyncVar(hook = nameof(HookP2PopulationLimit))] int syncP2PopulationLimit;

        bool busRegistered;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[RtsUtsSupplyStateSync] Trùng instance — giữ object đầu tiên.");
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

            UnregisterBus();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            RegisterBusIfNeeded();
            PushServerSnapshotToSyncVars();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!isServer)
            {
                ApplyOwnerSnapshot(Owner.Player1);
                ApplyOwnerSnapshot(Owner.Player2);
            }
        }

        public override void OnStopServer()
        {
            UnregisterBus();
            base.OnStopServer();
        }

        /// <summary>
        /// Mục tiêu: Gắn sync supplies lên GameplayMapCore khi server vào scene trận.
        /// Cách hoạt động: Tìm component scene; nếu thiếu thì thêm lên core (không spawn GameObject lẻ — Mirror từ chối).
        /// </summary>
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

            RtsUtsSupplyStateSync existing = Object.FindFirstObjectByType<RtsUtsSupplyStateSync>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                return;
            }

            GameplayMapCoreMarker core = Object.FindFirstObjectByType<GameplayMapCoreMarker>(
                FindObjectsInactive.Include);
            GameObject host = core != null ? core.gameObject : new GameObject(nameof(RtsUtsSupplyStateSync));

            if (host.GetComponent<NetworkIdentity>() == null)
            {
                host.AddComponent<NetworkIdentity>();
            }

            if (host.GetComponent<RtsUtsSupplyStateSync>() == null)
            {
                host.AddComponent<RtsUtsSupplyStateSync>();
            }

            if (host.GetComponent<NetworkIdentity>().netId == 0)
            {
                NetworkServer.Spawn(host);
            }
            else if (core == null)
            {
                NetworkServer.Spawn(host);
            }
        }

        /// <summary>
        /// Mục tiêu: Pure client — áp snapshot supplies khi sync object đã replicate (retry bootstrap).
        /// Cách hoạt động: Gọi ApplyOwnerSnapshot cho LocalOwner nếu Instance tồn tại.
        /// </summary>
        public static void ClientRefreshLocalSuppliesIfReady()
        {
            if (!NetworkClient.active || NetworkServer.active || Instance == null)
            {
                return;
            }

            Owner local = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            if (!HumanFogVisionUtility.EmitsFogVision(local))
            {
                return;
            }

            Instance.ApplyOwnerSnapshot(local);
        }

        void RegisterBusIfNeeded()
        {
            if (busRegistered)
            {
                return;
            }

            Bus<SupplyEvent>.RegisterForAll(HandleServerSupplyEvent);
            Bus<UnitSpawnEvent>.RegisterForAll(HandleUnitSpawn);
            Bus<UnitDeathEvent>.RegisterForAll(HandleUnitDeath);
            busRegistered = true;
        }

        void UnregisterBus()
        {
            if (!busRegistered)
            {
                return;
            }

            Bus<SupplyEvent>.UnregisterForAll(HandleServerSupplyEvent);
            Bus<UnitSpawnEvent>.UnregisterForAll(HandleUnitSpawn);
            Bus<UnitDeathEvent>.UnregisterForAll(HandleUnitDeath);
            busRegistered = false;
        }

        void HandleServerSupplyEvent(SupplyEvent evt)
        {
            if (!isServer)
            {
                return;
            }

            Supplies.ServerAuthoritativeSupplyEvent(evt);
            PushServerSnapshotToSyncVars();

            if (evt.Amount > 0 && evt.Supply != null)
            {
                SupplyGainKind kind = SupplyGainKindResolver.Resolve(evt.Supply);
                if (kind != SupplyGainKind.Unknown)
                {
                    GameMatchOverlayStateSync.BroadcastSupplyGain(evt.Owner, kind, evt.Amount);
                }
            }
        }

        void HandleUnitSpawn(UnitSpawnEvent evt)
        {
            if (!isServer)
            {
                return;
            }

            PushServerSnapshotToSyncVars();
        }

        void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (!isServer)
            {
                return;
            }

            PushServerSnapshotToSyncVars();
        }

        void PushServerSnapshotToSyncVars()
        {
            Supplies.EnsureReady();
            ReadOwner(Owner.Player1, out syncP1Stone, out syncP1Wood, out syncP1Food, out syncP1Population, out syncP1PopulationLimit);
            ReadOwner(Owner.Player2, out syncP2Stone, out syncP2Wood, out syncP2Food, out syncP2Population, out syncP2PopulationLimit);

            GameMatchOverlayStateSync.BroadcastSupplySnapshot(
                Owner.Player1,
                syncP1Stone,
                syncP1Wood,
                syncP1Food,
                syncP1Population,
                syncP1PopulationLimit);
            GameMatchOverlayStateSync.BroadcastSupplySnapshot(
                Owner.Player2,
                syncP2Stone,
                syncP2Wood,
                syncP2Food,
                syncP2Population,
                syncP2PopulationLimit);
        }

        static void ReadOwner(
            Owner owner,
            out int stone,
            out int wood,
            out int food,
            out int population,
            out int populationLimit)
        {
            stone = Supplies.Stone[owner];
            wood = Supplies.Wood[owner];
            food = Supplies.Food[owner];
            population = Supplies.Population[owner];
            populationLimit = Supplies.PopulationLimit[owner];
        }

        void HookP1Stone(int _, int value) => ApplyOwnerSnapshot(Owner.Player1);
        void HookP1Wood(int _, int value) => ApplyOwnerSnapshot(Owner.Player1);
        void HookP1Food(int _, int value) => ApplyOwnerSnapshot(Owner.Player1);
        void HookP1Population(int _, int value) => ApplyOwnerSnapshot(Owner.Player1);
        void HookP1PopulationLimit(int _, int value) => ApplyOwnerSnapshot(Owner.Player1);
        void HookP2Stone(int _, int value) => ApplyOwnerSnapshot(Owner.Player2);
        void HookP2Wood(int _, int value) => ApplyOwnerSnapshot(Owner.Player2);
        void HookP2Food(int _, int value) => ApplyOwnerSnapshot(Owner.Player2);
        void HookP2Population(int _, int value) => ApplyOwnerSnapshot(Owner.Player2);
        void HookP2PopulationLimit(int _, int value) => ApplyOwnerSnapshot(Owner.Player2);

        void ApplyOwnerSnapshot(Owner owner)
        {
            if (isServer)
            {
                return;
            }

            Supplies.EnsureReady();
            Supplies.ApplyNetworkSnapshot(
                owner,
                owner == Owner.Player1 ? syncP1Stone : syncP2Stone,
                owner == Owner.Player1 ? syncP1Wood : syncP2Wood,
                owner == Owner.Player1 ? syncP1Food : syncP2Food,
                owner == Owner.Player1 ? syncP1Population : syncP2Population,
                owner == Owner.Player1 ? syncP1PopulationLimit : syncP2PopulationLimit);
        }
    }
}

using System.Collections.Generic;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Đồng bộ hàng đợi train/research và tiến độ xây nhà MP.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(BaseBuilding))]
    public sealed class RtsUtsNetworkBuildingSync : NetworkBehaviour
    {
        readonly SyncList<string> syncQueue = new SyncList<string>();

        [SyncVar(hook = nameof(HookQueueHead))]
        string syncSoBeingBuiltName = string.Empty;

        [SyncVar(hook = nameof(HookQueueStartTime))]
        float syncQueueStartTime;

        [SyncVar(hook = nameof(HookProgressState))]
        byte syncProgressState;

        [SyncVar(hook = nameof(HookProgressCompletion))]
        float syncProgressCompletion;

        [SyncVar(hook = nameof(HookProgressStartTime))]
        float syncProgressStartTime;

        BaseBuilding _building;
        float _nextServerPushTime;
        bool _queueHooksRegistered;

        void Awake()
        {
            _building = GetComponent<BaseBuilding>();
        }

        void OnDisable()
        {
            UnregisterQueueHooks();
        }

        void Update()
        {
            if (!RtsNetplayNetworkBehaviourUtility.CanPushServerState(this) || _building == null)
            {
                return;
            }

            if (Time.time < _nextServerPushTime)
            {
                return;
            }

            _nextServerPushTime = Time.time + 0.2f;
            ServerPushFullState();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            RegisterQueueHooks();
            ServerPushFullState();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            RegisterQueueHooks();
            if (!NetworkServer.active)
            {
                ApplyPresentationFromNetwork();
            }
        }

        /// <summary>
        /// Mục tiêu: Client yêu cầu hủy slot hàng đợi trên server.
        /// </summary>
        public void RequestCancelQueueIndex(int index)
        {
            if (!RtsNetplaySession.IsPureClient)
            {
                return;
            }

            CmdCancelQueueIndex(index);
        }

        [Command(requiresAuthority = false)]
        void CmdCancelQueueIndex(int index, Mirror.NetworkConnectionToClient sender = null)
        {
            if (_building == null || sender == null)
            {
                return;
            }

            if (!TryGetComponent(out RtsUtsNetworkEntity networkEntity)
                || !networkEntity.ServerCanAcceptOrdersFrom(sender.connectionId))
            {
                return;
            }

            _building.CancelBuildingUnit(index);
            ServerPushFullState();
        }

        [Server]
        public void ServerPushFullState()
        {
            if (!RtsNetplayNetworkBehaviourUtility.CanPushServerState(this) || _building == null)
            {
                return;
            }

            syncQueue.Clear();
            UnlockableSO[] queue = _building.Queue;
            for (int i = 0; i < queue.Length; i++)
            {
                if (queue[i] != null)
                {
                    syncQueue.Add(queue[i].name);
                }
            }

            syncSoBeingBuiltName = _building.SOBeingBuilt != null ? _building.SOBeingBuilt.name : string.Empty;
            syncQueueStartTime = _building.CurrentQueueStartTime;
            syncProgressState = (byte)_building.Progress.State;
            syncProgressCompletion = _building.Progress.Completion;
            syncProgressStartTime = _building.Progress.StartTime;
        }

        void RegisterQueueHooks()
        {
            if (_queueHooksRegistered)
            {
                return;
            }

            syncQueue.Callback += OnQueueChanged;
            _queueHooksRegistered = true;
        }

        void UnregisterQueueHooks()
        {
            if (!_queueHooksRegistered)
            {
                return;
            }

            syncQueue.Callback -= OnQueueChanged;
            _queueHooksRegistered = false;
        }

        void OnQueueChanged(SyncList<string>.Operation op, int index, string oldItem, string newItem) =>
            ApplyPresentationFromNetwork();

        void HookQueueHead(string _, string __) => ApplyPresentationFromNetwork();

        void HookQueueStartTime(float _, float __) => ApplyPresentationFromNetwork();

        void HookProgressState(byte _, byte __) => ApplyPresentationFromNetwork();

        void HookProgressCompletion(float _, float __) => ApplyPresentationFromNetwork();

        void HookProgressStartTime(float _, float __) => ApplyPresentationFromNetwork();

        void ApplyPresentationFromNetwork()
        {
            if (NetworkServer.active || _building == null || !RtsNetplayNetworkBehaviourUtility.IsSpawned(this))
            {
                return;
            }

            var queue = new List<UnlockableSO>(syncQueue.Count);
            for (int i = 0; i < syncQueue.Count; i++)
            {
                if (RtsUnlockableAssetCatalog.TryResolveUnlockable(syncQueue[i], out UnlockableSO unlockable))
                {
                    queue.Add(unlockable);
                }
            }

            UnlockableSO soBeingBuilt = null;
            if (!string.IsNullOrWhiteSpace(syncSoBeingBuiltName))
            {
                RtsUnlockableAssetCatalog.TryResolveUnlockable(syncSoBeingBuiltName, out soBeingBuilt);
            }

            var progress = new BuildingProgress(
                (BuildingProgress.BuildingState)syncProgressState,
                syncProgressStartTime,
                syncProgressCompletion);

            _building.ApplyNetworkPresentationState(queue, soBeingBuilt, syncQueueStartTime, progress);
        }
    }
}

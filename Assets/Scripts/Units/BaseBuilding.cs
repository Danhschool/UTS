using System.Collections;
using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Utilities;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Units
{
    public class BaseBuilding : AbstractCommandable
    {
        public int QueueSize => buildingQueue.Count;
        public UnlockableSO[] Queue => buildingQueue.ToArray();
        [field: SerializeField] public float CurrentQueueStartTime { get; private set; }
        [field: SerializeField] public UnlockableSO SOBeingBuilt { get; private set; }
        [field: SerializeField] public MeshRenderer MainRenderer { get; private set; }
        [field: SerializeField]
        public BuildingProgress Progress { get; private set; } = new(
            BuildingProgress.BuildingState.Destroyed, 0, 0
        );
        [field: SerializeField] public BuildingSO BuildingSO { get; private set; }
        [SerializeField] private NavMeshObstacle navMeshObstacle;
        [Tooltip("Kéo child empty (ví dụ cửa nhà) nằm trên NavMesh. Để trống thì unit spawn tại vị trí building.")]
        [SerializeField] private Transform unitSpawnPoint;

        public delegate void QueueUpdatedEvent(UnlockableSO[] unitsInQueue);
        public event QueueUpdatedEvent OnQueueUpdated;

        private Placeholder culledVisuals;
        private IBuildingBuilder unitBuildingThis;
        private List<UnlockableSO> buildingQueue = new(MAX_QUEUE_SIZE);
        private const int MAX_QUEUE_SIZE = 5;
        private IBuildingPassiveEffect[] passiveEffects;
        private bool buildingSpawnEventRaised;
        private bool networkConstructionVisualActive;
        private bool networkConstructionUsePredictedProgress;
        private float networkConstructionPredictedStartTime;
        private float networkConstructionBuildTimeSeconds;
        private Vector3 networkConstructionStartWorld;
        private Vector3 networkConstructionEndWorld;

        protected override void Awake()
        {
            base.Awake();

            BuildingSO = UnitSO as BuildingSO;
            MaxHealth = BuildingSO.Health;
            // Current health is set as the building is being built via Heal()
            passiveEffects = GetComponents<IBuildingPassiveEffect>();
            SyncPassiveEffects(false);
        }

        private void OnEnable()
        {
            SyncPassiveEffects(true);
        }

        private void OnDisable()
        {
            SyncPassiveEffects(false);
            networkConstructionVisualActive = false;
            networkConstructionUsePredictedProgress = false;
            networkConstructionBuildTimeSeconds = 0f;
        }

        void LateUpdate()
        {
            if (!RtsNetplaySession.IsPureClient || !networkConstructionVisualActive)
            {
                return;
            }

            float completion = Progress.Completion;
            if (networkConstructionUsePredictedProgress
                && Progress.State != BuildingProgress.BuildingState.Completed
                && networkConstructionBuildTimeSeconds > 0f)
            {
                float predicted =
                    (Time.time - networkConstructionPredictedStartTime) / networkConstructionBuildTimeSeconds;
                completion = Mathf.Clamp01(Mathf.Max(completion, predicted));
            }

            ApplyNetworkConstructionVisualPosition(completion);
        }

        /// <summary>
        /// Mục tiêu: Bật/tắt food gen, tháp bắn, … cùng lúc với BaseBuilding (đang xây thì tắt).
        /// Cách hoạt động: Gọi <see cref="IBuildingPassiveEffect.SetEffectActive"/> trên mọi implementation cùng GameObject.
        /// </summary>
        private void SyncPassiveEffects(bool isActive)
        {
            if (passiveEffects == null)
            {
                return;
            }

            for (int i = 0; i < passiveEffects.Length; i++)
            {
                passiveEffects[i]?.SetEffectActive(isActive);
            }
        }

        protected override void Start()
        {
            base.Start();

            bool underConstruction = Progress.State == BuildingProgress.BuildingState.Building
                || Progress.State == BuildingProgress.BuildingState.Paused;
            bool deferToNetworkSync = RtsNetplaySession.IsPureClient
                && TryGetComponent<NetworkIdentity>(out _);

            if (underConstruction || deferToNetworkSync)
            {
                return;
            }

            Progress = new BuildingProgress(BuildingProgress.BuildingState.Completed, Progress.StartTime, 1);
            unitBuildingThis = null;
            Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;
            if (!(RtsNetplaySession.IsPureClient && TryGetComponent<NetworkIdentity>(out _)))
            {
                RaiseBuildingSpawnEventIfNeeded();
            }

            foreach (UpgradeSO upgrade in BuildingSO.Upgrades)
            {
                if (BuildingSO.TechTree.IsResearched(Owner, upgrade))
                {
                    upgrade.Apply(BuildingSO);
                }
            }

            SyncRuntimeHealthFromUnitSo(healAddedMaxPortion: true);
            if (MaxHealth > 0 && CurrentHealth <= 0)
            {
                Heal(MaxHealth);
            }

            RefreshVisionFromSightConfig();
        }

        /// <summary>
        /// Mục tiêu: Civil Central spawn đầu trận MP luôn Completed trước khi client nhận sync đầu tiên.
        /// Cách hoạt động: Server gọi ngay sau spawn — set progress, heal, passive, push BuildingSpawnEvent + SyncVar.
        /// </summary>
        internal void EnsureCivilCentralMatchStartReady()
        {
            if (!CivilCentralUtility.IsCivilCentral(this))
            {
                return;
            }

            Progress = new BuildingProgress(BuildingProgress.BuildingState.Completed, Time.time, 1f);
            unitBuildingThis = null;
            Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;
            SyncPassiveEffects(true);
            SyncRuntimeHealthFromUnitSo(healAddedMaxPortion: true);

            if (MaxHealth > 0 && CurrentHealth < MaxHealth)
            {
                Heal(MaxHealth - CurrentHealth);
            }

            foreach (UpgradeSO upgrade in BuildingSO.Upgrades)
            {
                if (BuildingSO.TechTree.IsResearched(Owner, upgrade))
                {
                    upgrade.Apply(BuildingSO);
                }
            }

            EnsureGameplayBehaviourActive();
            RaiseBuildingSpawnEventIfNeeded();
            RefreshVisionFromSightConfig();
            NotifyNetworkBuildingStateIfServer();
        }

        /// <summary>
        /// Mục tiêu: Prefab nhà (trừ CC) để BaseBuilding tắt — MP/offline cần bật lại để queue, passive, UI hoạt động.
        /// Cách hoạt động: Set enabled trên MonoBehaviour khi nhà vào gameplay thật (spawn CC / xây xong).
        /// </summary>
        internal void EnsureGameplayBehaviourActive()
        {
            if (!enabled)
            {
                enabled = true;
            }
        }
        /// <summary>
        /// Cách hoạt động: Đã researched (TechTree) hoặc đã có trong buildingQueue → false.
        /// </summary>
        public bool CanEnqueueUpgrade(UpgradeSO upgrade)
        {
            if (upgrade == null || BuildingSO?.TechTree == null)
            {
                return false;
            }

            if (!upgrade.IsOneTimeUnlock)
            {
                return true;
            }

            if (BuildingSO.TechTree.IsResearched(Owner, upgrade))
            {
                return false;
            }

            return !ContainsInQueue(upgrade);
        }

        public bool ContainsInQueue(UnlockableSO unlockable)
        {
            if (unlockable == null)
            {
                return false;
            }

            for (int i = 0; i < buildingQueue.Count; i++)
            {
                if (buildingQueue[i] == unlockable)
                {
                    return true;
                }
            }

            return false;
        }

        public void BuildUnlockable(UnlockableSO unlockable)
        {
            if (RtsNetplaySession.IsNetworkMatch && !NetworkServer.active)
            {
                return;
            }

            if (buildingQueue.Count == MAX_QUEUE_SIZE)
            {
                Debug.LogError("BuildUnit called when the queue was already full! This is not supported!");
                return;
            }

            if (unlockable is UpgradeSO upgrade && !CanEnqueueUpgrade(upgrade))
            {
                return;
            }

            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -unlockable.Cost.Stone, unlockable.Cost.StoneSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -unlockable.Cost.Wood, unlockable.Cost.WoodSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -unlockable.Cost.Food, unlockable.Cost.FoodSO));

            buildingQueue.Add(unlockable);
            if (buildingQueue.Count == 1 && RtsNetplaySession.ShouldRunAuthoritativeGameplay)
            {
                StartCoroutine(DoBuildUnits());
            }

            OnQueueUpdated?.Invoke(buildingQueue.ToArray());
            NotifyNetworkBuildingStateIfServer();
        }

        public void CancelBuildingUnit(int index)
        {
            if (RtsNetplaySession.IsPureClient && TryGetComponent(out RtsUtsNetworkBuildingSync buildingSync))
            {
                buildingSync.RequestCancelQueueIndex(index);
                return;
            }

            if (RtsNetplaySession.IsNetworkMatch && !NetworkServer.active)
            {
                return;
            }

            if (index < 0 || index >= buildingQueue.Count)
            {
                Debug.LogError("Attempting to cancel building a unit outside the bounds of the queue!");
                return;
            }

            UnlockableSO unlockableSO = buildingQueue[index];
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, unlockableSO.Cost.Stone, unlockableSO.Cost.StoneSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, unlockableSO.Cost.Wood, unlockableSO.Cost.WoodSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, unlockableSO.Cost.Food, unlockableSO.Cost.FoodSO));
            buildingQueue.RemoveAt(index);
            if (index == 0)
            {
                StopAllCoroutines();

                if (buildingQueue.Count > 0)
                {
                    StartCoroutine(DoBuildUnits());
                }
                else
                {
                    OnQueueUpdated?.Invoke(buildingQueue.ToArray());
                }
            }
            else
            {
                OnQueueUpdated?.Invoke(buildingQueue.ToArray());
            }

            NotifyNetworkBuildingStateIfServer();
        }

        /// <summary>
        /// Mục tiêu: Client MP hiển thị queue/progress khớp server.
        /// </summary>
        internal void ApplyNetworkPresentationState(
            IReadOnlyList<UnlockableSO> queueSnapshot,
            UnlockableSO soBeingBuilt,
            float queueStartTime,
            BuildingProgress progress)
        {
            buildingQueue.Clear();
            if (queueSnapshot != null)
            {
                for (int i = 0; i < queueSnapshot.Count; i++)
                {
                    if (queueSnapshot[i] != null)
                    {
                        buildingQueue.Add(queueSnapshot[i]);
                    }
                }
            }

            SOBeingBuilt = soBeingBuilt;
            CurrentQueueStartTime = queueStartTime;

            BuildingProgress previous = Progress;
            progress = SanitizeClientCivilCentralProgress(progress);
            Progress = progress;

            if (previous.State != BuildingProgress.BuildingState.Completed
                && progress.State == BuildingProgress.BuildingState.Completed)
            {
                ApplyConstructionCompletedPresentationFromNetwork();
            }
            else if (RtsNetplaySession.IsPureClient
                     && progress.State == BuildingProgress.BuildingState.Completed
                     && !buildingSpawnEventRaised)
            {
                ApplyConstructionCompletedPresentationFromNetwork();
            }
            else if (RtsNetplaySession.IsPureClient)
            {
                RefreshNetworkConstructionVisualState(progress);
            }

            OnQueueUpdated?.Invoke(buildingQueue.ToArray());
        }

        /// <summary>
        /// Mục tiêu: CC client không bị sync Destroyed mặc định ghi đè Completed từ server/prefab.
        /// Cách hoạt động: Pure client + Civil Central + progress Destroyed → ép Completed.
        /// </summary>
        BuildingProgress SanitizeClientCivilCentralProgress(BuildingProgress progress)
        {
            if (!RtsNetplaySession.IsPureClient
                || !CivilCentralUtility.IsCivilCentral(this)
                || progress.State != BuildingProgress.BuildingState.Destroyed)
            {
                return progress;
            }

            return new BuildingProgress(
                BuildingProgress.BuildingState.Completed,
                progress.StartTime > 0f ? progress.StartTime : Time.time,
                1f);
        }

        /// <summary>
        /// Mục tiêu: Client MP — nhà nổi từ dưới đất theo Completion giống PvE offline.
        /// Cách hoạt động: Lần đầu thấy Building/Paused thì cache vị trí chôn/đích; LateUpdate lerp theo Completion.
        /// </summary>
        void RefreshNetworkConstructionVisualState(BuildingProgress progress)
        {
            if (CivilCentralUtility.IsCivilCentral(this))
            {
                networkConstructionVisualActive = false;
                return;
            }

            bool underConstruction = progress.State == BuildingProgress.BuildingState.Building
                || progress.State == BuildingProgress.BuildingState.Paused;

            if (!underConstruction)
            {
                if (!networkConstructionUsePredictedProgress)
                {
                    networkConstructionVisualActive = false;
                }

                return;
            }

            if (!networkConstructionVisualActive)
            {
                InitializeNetworkConstructionVisualAnchors(progress.Completion);
            }

            ApplyNetworkConstructionVisualPosition(progress.Completion);
        }

        void InitializeNetworkConstructionVisualAnchors(float completion)
        {
            if (networkConstructionEndWorld != Vector3.zero
                && networkConstructionStartWorld != Vector3.zero)
            {
                networkConstructionVisualActive = true;
                ApplyNetworkConstructionVisualPosition(completion);
                return;
            }

            float buryDepth = MainRenderer != null
                ? Mathf.Max(MainRenderer.bounds.size.y, 0.5f)
                : 0.5f;
            float t = Mathf.Clamp01(completion);
            Vector3 current = transform.position;
            networkConstructionEndWorld = current + Vector3.up * buryDepth * (1f - t);
            networkConstructionStartWorld = networkConstructionEndWorld - Vector3.up * buryDepth;
            networkConstructionVisualActive = true;
        }

        /// <summary>
        /// Mục tiêu: Client MP — anchor animation xây đúng vị trí đặt (server không sync transform nhà).
        /// Cách hoạt động: RpcLink truyền targetLocation; đặt start chôn / end mặt đất rồi lerp theo Completion.
        /// </summary>
        internal void SeedClientConstructionPresentationAnchor(
            Vector3 endWorld,
            float syncedCompletion,
            float buildTimeSeconds)
        {
            if (!RtsNetplaySession.IsPureClient)
            {
                return;
            }

            float buryDepth = MainRenderer != null
                ? Mathf.Max(MainRenderer.bounds.size.y, 0.5f)
                : 0.5f;
            networkConstructionEndWorld = endWorld;
            networkConstructionStartWorld = endWorld + Vector3.down * buryDepth;
            networkConstructionVisualActive = true;

            float clampedCompletion = Mathf.Clamp01(syncedCompletion);
            networkConstructionPredictedStartTime = buildTimeSeconds > 0f
                ? Time.time - clampedCompletion * buildTimeSeconds
                : Time.time;
            networkConstructionUsePredictedProgress = buildTimeSeconds > 0f;
            networkConstructionBuildTimeSeconds = buildTimeSeconds > 0f ? buildTimeSeconds : 0f;

            float initialCompletion = clampedCompletion;
            if (networkConstructionUsePredictedProgress)
            {
                initialCompletion = Mathf.Clamp01((Time.time - networkConstructionPredictedStartTime) / buildTimeSeconds);
            }

            ApplyNetworkConstructionVisualPosition(initialCompletion);
        }

        void ApplyNetworkConstructionVisualPosition(float completion)
        {
            if (!networkConstructionVisualActive)
            {
                return;
            }

            float t = Mathf.Clamp01(completion);
            transform.position = Vector3.Lerp(networkConstructionStartWorld, networkConstructionEndWorld, t);
        }

        /// <summary>
        /// Mục tiêu: Client MP hoàn tất presentation khi SyncVar báo Completed (không gọi server notify).
        /// Cách hoạt động: Gỡ builder, bật passive, heal đầy, raise BuildingSpawnEvent một lần.
        /// </summary>
        void ApplyConstructionCompletedPresentationFromNetwork()
        {
            EnsureGameplayBehaviourActive();
            unitBuildingThis = null;
            Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;
            SyncPassiveEffects(true);

            networkConstructionUsePredictedProgress = false;

            if (networkConstructionVisualActive)
            {
                transform.position = networkConstructionEndWorld;
                networkConstructionVisualActive = false;
            }
            else if (networkConstructionEndWorld != Vector3.zero)
            {
                transform.position = networkConstructionEndWorld;
            }

            if (MaxHealth > 0 && CurrentHealth < MaxHealth)
            {
                Heal(MaxHealth - CurrentHealth);
            }

            RaiseBuildingSpawnEventIfNeeded();
            RefreshVisionFromSightConfig();
        }

        /// <summary>
        /// Mục tiêu: Client MP — xác nhận xây xong khi SyncVar trễ hoặc không hook (Rpc từ server).
        /// </summary>
        internal void ForceClientConstructionCompletedPresentation()
        {
            if (!RtsNetplaySession.IsPureClient)
            {
                return;
            }

            BuildingProgress previous = Progress;
            Progress = new BuildingProgress(
                BuildingProgress.BuildingState.Completed,
                Progress.StartTime,
                1f);

            if (previous.State == BuildingProgress.BuildingState.Completed)
            {
                if (!buildingSpawnEventRaised)
                {
                    EnsureGameplayBehaviourActive();
                    RaiseBuildingSpawnEventIfNeeded();
                }

                return;
            }

            ApplyConstructionCompletedPresentationFromNetwork();
        }

        /// <summary>
        /// Mục tiêu: Server đẩy tiến độ xây lên client trong lúc BuildBuildingAction chạy.
        /// Cách hoạt động: Cập nhật Completion khi state vẫn là Building rồi notify sync.
        /// </summary>
        internal void SetConstructionCompletion(float completion)
        {
            if (Progress.State != BuildingProgress.BuildingState.Building)
            {
                return;
            }

            float clamped = Mathf.Clamp01(completion);
            if (Mathf.Abs(Progress.Completion - clamped) < 0.02f)
            {
                return;
            }

            Progress = new BuildingProgress(
                BuildingProgress.BuildingState.Building,
                Progress.StartTime,
                clamped);
            NotifyNetworkBuildingStateIfServer();
        }

        void NotifyNetworkBuildingStateIfServer()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            if (TryGetComponent(out RtsUtsNetworkBuildingSync buildingSync))
            {
                buildingSync.ServerPushFullState();
            }
        }

        public void StartBuilding(IBuildingBuilder buildingBuilder)
        {
            EnsureGameplayBehaviourActive();
            Awake();
            unitBuildingThis = buildingBuilder;
            Owner = unitBuildingThis.Owner;

            Progress = new BuildingProgress(
                BuildingProgress.BuildingState.Building,
                Time.time - BuildingSO.BuildTime * Progress.Completion,
                Progress.Completion
            );

            if (Progress.Completion == 0)
            {
                // Tránh trường hợp Resume/StartBuilding được gọi nhiều lần khi CurrentHealth đã có sẵn.
                if (CurrentHealth <= 0)
                {
                    Heal(1);
                }
            }

            Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;
            Bus<UnitDeathEvent>.OnEvent[Owner] += HandleUnitDeath;
            NotifyNetworkBuildingStateIfServer();
        }

        /// <summary>
        /// Mục tiêu: Worker rời công trường (lệnh move/gather…) mà không hủy nhà — giữ tiến độ để resume sau.
        /// Cách hoạt động: Nếu đang Building thì tính Completion, đặt Paused, gỡ builder và đăng ký chết.
        /// </summary>
        public void PauseConstructionAndReleaseBuilder()
        {
            if (Progress.State != BuildingProgress.BuildingState.Building)
            {
                return;
            }

            float completion = Mathf.Clamp01((Time.time - Progress.StartTime) / BuildingSO.BuildTime);
            Progress = new BuildingProgress(
                BuildingProgress.BuildingState.Paused,
                Progress.StartTime,
                completion
            );
            unitBuildingThis = null;
            Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;
        }

        /// <summary>
        /// Mục tiêu: Cho phép UI/lệnh build biết công trình này có thể tiếp tục.
        /// Cách hoạt động: True khi state Paused (hoặc Destroyed placeholder theo data cũ).
        /// </summary>
        public bool CanResumeConstruction() =>
            Progress.State == BuildingProgress.BuildingState.Paused
            || Progress.State == BuildingProgress.BuildingState.Destroyed;

        /// <summary>
        /// Mục tiêu: Đánh dấu công trình đã xây xong để queue research/unit và passive effects hoạt động.
        /// Cách hoạt động: Đặt Progress Completed, gỡ builder, bật passive; bỏ qua nếu đã Completed.
        /// </summary>
        public void CompleteConstruction()
        {
            if (Progress.State == BuildingProgress.BuildingState.Completed)
            {
                return;
            }

            Progress = new BuildingProgress(
                BuildingProgress.BuildingState.Completed,
                Progress.StartTime,
                1f);
            unitBuildingThis = null;
            Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;
            SyncPassiveEffects(true);

            if (MaxHealth > 0 && CurrentHealth < MaxHealth)
            {
                Heal(MaxHealth - CurrentHealth);
            }

            EnsureGameplayBehaviourActive();
            RaiseBuildingSpawnEventIfNeeded();
            NotifyNetworkBuildingStateIfServer();
        }

        /// <summary>
        /// Mục tiêu: Tech tree / UI biết nhà đã hoạt động (một lần — tránh trùng khi Start chạy sau xây xong).
        /// Cách hoạt động: Raise <see cref="BuildingSpawnEvent"/> nếu Owner hợp lệ và chưa raise.
        /// </summary>
        private void RaiseBuildingSpawnEventIfNeeded()
        {
            if (buildingSpawnEventRaised)
            {
                return;
            }

            buildingSpawnEventRaised = true;
            Bus<BuildingSpawnEvent>.Raise(Owner, new BuildingSpawnEvent(Owner, this));
        }

        /// <summary>
        /// Mục tiêu: Client MP đăng ký nhà spawn qua network (minimap, UI, fog).
        /// Cách hoạt động: Gọi RaiseBuildingSpawnEventIfNeeded sau khi Owner sync.
        /// </summary>
        public void NotifyNetworkSpawnPresentation()
        {
            if (Progress.State != BuildingProgress.BuildingState.Completed)
            {
                return;
            }

            EnsureGameplayBehaviourActive();
            RaiseBuildingSpawnEventIfNeeded();
        }

        /// <summary>
        /// Mục tiêu: Client MP ẩn nhà chết trước khi Mirror unspawn.
        /// Cách hoạt động: Tắt collider/renderer; không raise BuildingDeathEvent (server đã xử lý tech).
        /// </summary>
        public void ExecuteNetworkDeathPresentation()
        {
            if (IsInDeathSequence)
            {
                return;
            }

            MarkDeathSequenceStarted();

            if (IsSelected)
            {
                Deselect();
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = false;
                }
            }
        }

        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            if (evt.Unit.TryGetComponent(out IBuildingBuilder buildingBuilder) && buildingBuilder == unitBuildingThis)
            {
                Progress = new BuildingProgress(
                    BuildingProgress.BuildingState.Paused,
                    Progress.StartTime,
                    (Time.time - Progress.StartTime) / BuildingSO.BuildTime
                );

                Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;
            }
        }

        private IEnumerator DoBuildUnits()
        {
            while (buildingQueue.Count > 0)
            {
                SOBeingBuilt = buildingQueue[0];
                CurrentQueueStartTime = RtsNetplaySession.IsNetworkMatch
                    ? (float)NetworkTime.time
                    : Time.time;
                OnQueueUpdated?.Invoke(buildingQueue.ToArray());

                yield return new WaitForSeconds(SOBeingBuilt.BuildTime);

                if (SOBeingBuilt is AbstractUnitSO unitSO)
                {
                    Vector3 spawn = GetUnitSpawnWorldPosition();
                    Quaternion rotation = UnitSpawnWorldRotation;

                    if (RtsNetplaySession.IsNetworkMatch)
                    {
                        NetworkConnectionToClient connection = null;
                        if (TryGetComponent(out RtsUtsNetworkEntity networkEntity))
                        {
                            connection = networkEntity.ResolveOwnerConnection();
                        }

                        if (!RtsUtsServerEntityFactory.TrySpawnUnit(
                                unitSO,
                                spawn,
                                rotation,
                                Owner,
                                connection,
                                out _))
                        {
                            Debug.LogError(
                                $"[BaseBuilding] MP spawn unit thất bại: {unitSO.name} tại {spawn}. Kiểm tra NetworkIdentity + spawnPrefabs.");
                        }
                    }
                    else
                    {
                        GameObject instance = Instantiate(unitSO.Prefab, spawn, rotation);
                        if (instance.TryGetComponent(out AbstractUnit unit))
                        {
                            unit.Owner = Owner;
                            unit.NotifySpawned();
                        }
                    }
                }
                else if (SOBeingBuilt is UpgradeSO upgrade)
                {
                    Bus<UpgradeResearchedEvent>.Raise(Owner, new UpgradeResearchedEvent(Owner, upgrade));
                }

                buildingQueue.RemoveAt(0);
                NotifyNetworkBuildingStateIfServer();
            }

            OnQueueUpdated?.Invoke(buildingQueue.ToArray());
            NotifyNetworkBuildingStateIfServer();
        }

        /// <summary>Vị trí spawn unit trên rìa CC/nhà (world space).</summary>
        public Vector3 UnitSpawnWorldPosition => GetUnitSpawnWorldPosition();

        /// <summary>Hướng spawn unit theo <see cref="unitSpawnPoint"/> hoặc building.</summary>
        public Quaternion UnitSpawnWorldRotation =>
            unitSpawnPoint != null ? unitSpawnPoint.rotation : transform.rotation;

        /// <summary>
        /// Mục tiêu: Cho biết world position spawn unit từ hàng đợi nhà (theo điểm bạn chọn trong Inspector hoặc pivot nhà).
        /// Cách hoạt động: Nếu có <see cref="unitSpawnPoint"/> thì trả về vị trí của nó, không thì dùng <see cref="Transform.position"/> của building.
        /// </summary>
        private Vector3 GetUnitSpawnWorldPosition() =>
            unitSpawnPoint != null ? unitSpawnPoint.position : transform.position;

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;
            Bus<BuildingDeathEvent>.Raise(Owner, new BuildingDeathEvent(Owner, this));
        }

        protected override void OnGainVisibility()
        {
            base.OnGainVisibility();
            if (culledVisuals != null)
            {
                culledVisuals.gameObject.SetActive(false);
            }
        }

        protected override void OnLoseVisibility()
        {
            base.OnLoseVisibility();

            if (MainRenderer == null
                || Progress.State != BuildingProgress.BuildingState.Completed)
            {
                return;
            }

            MeshFilter sourceFilter = MainRenderer.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                return;
            }

            if (culledVisuals == null)
            {
                Transform originalRendererTransform = MainRenderer.transform;
                GameObject culledGO = new($"Culled {BuildingSO.Name} Visuals")
                {
                    transform =
                    {
                        position = originalRendererTransform.position,
                        rotation = originalRendererTransform.rotation,
                        localScale = originalRendererTransform.localScale
                    }
                };

                int transparentFxLayer = LayerMask.NameToLayer("TransparentFX");
                culledGO.layer = transparentFxLayer >= 0 ? transparentFxLayer : 0;

                culledVisuals = culledGO.AddComponent<Placeholder>();
                culledVisuals.Owner = Owner;
                culledVisuals.ParentObject = gameObject;
                MeshFilter meshFilter = culledGO.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = sourceFilter.sharedMesh;
                MeshRenderer renderer = culledGO.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = MainRenderer.sharedMaterials;
            }
            else
            {
                culledVisuals.gameObject.SetActive(true);
            }
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
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

        protected override void Awake()
        {
            base.Awake();

            BuildingSO = UnitSO as BuildingSO;
            MaxHealth = BuildingSO.Health;
            // Current health is set as the building is being built via Heal()
        }

        protected override void Start()
        {
            base.Start();
            Progress = new BuildingProgress(BuildingProgress.BuildingState.Completed, Progress.StartTime, 1);
            unitBuildingThis = null;
            Bus<UnitDeathEvent>.OnEvent[Owner] -= HandleUnitDeath;
            Bus<BuildingSpawnEvent>.Raise(Owner, new BuildingSpawnEvent(Owner, this));

            foreach (UpgradeSO upgrade in BuildingSO.Upgrades)
            {
                if (BuildingSO.TechTree.IsResearched(Owner, upgrade))
                {
                    upgrade.Apply(BuildingSO);
                }
            }

            SyncRuntimeHealthFromUnitSo(healAddedMaxPortion: true);
            // Giống AbstractUnit: nhà hoàn thành phải có máu đầy. SyncRuntimeHealthFromUnitSo không tăng máu khi max không đổi
            // và CurrentHealth prefab = 0 → thanh máu / combat sai (CurrentHealth = 0).
            if (MaxHealth > 0 && CurrentHealth <= 0)
            {
                Heal(MaxHealth);
            }

            RefreshVisionFromSightConfig();
        }

        public void BuildUnlockable(UnlockableSO unlockable)
        {
            if (buildingQueue.Count == MAX_QUEUE_SIZE)
            {
                Debug.LogError("BuildUnit called when the queue was already full! This is not supported!");
                return;
            }

            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -unlockable.Cost.Stone, unlockable.Cost.StoneSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -unlockable.Cost.Wood, unlockable.Cost.WoodSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -unlockable.Cost.Food, unlockable.Cost.FoodSO));

            buildingQueue.Add(unlockable);
            if (buildingQueue.Count == 1)
            {
                StartCoroutine(DoBuildUnits());
            }
            else
            {
                OnQueueUpdated?.Invoke(buildingQueue.ToArray());
            }
        }

        public void CancelBuildingUnit(int index)
        {
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
        }

        public void StartBuilding(IBuildingBuilder buildingBuilder)
        {
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
                CurrentQueueStartTime = Time.time;
                OnQueueUpdated?.Invoke(buildingQueue.ToArray());

                yield return new WaitForSeconds(SOBeingBuilt.BuildTime);

                if (SOBeingBuilt is AbstractUnitSO unitSO)
                {
                    Vector3 spawn = GetUnitSpawnWorldPosition();
                    GameObject instance = Instantiate(unitSO.Prefab, spawn, Quaternion.identity);
                    if (instance.TryGetComponent(out AbstractCommandable commandable))
                    {
                        commandable.Owner = Owner;
                    }
                }
                else if (SOBeingBuilt is UpgradeSO upgrade)
                {
                    Bus<UpgradeResearchedEvent>.Raise(Owner, new UpgradeResearchedEvent(Owner, upgrade));
                }

                buildingQueue.RemoveAt(0);
            }

            OnQueueUpdated?.Invoke(buildingQueue.ToArray());
        }

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

            if (culledVisuals == null)
            {
                Transform originalRendererTransform = MainRenderer.transform;
                GameObject culledGO = new ($"Culled {BuildingSO.Name} Visuals")
                {
                    layer = LayerMask.GetMask("TransparentFX"),
                    transform =
                    {
                        position = originalRendererTransform.position,
                        rotation = originalRendererTransform.rotation,
                        localScale = originalRendererTransform.localScale
                    }
                };
                culledVisuals = culledGO.AddComponent<Placeholder>();
                culledVisuals.Owner = Owner;
                culledVisuals.ParentObject = gameObject;
                MeshFilter meshFilter = culledGO.AddComponent<MeshFilter>();
                meshFilter.mesh = MainRenderer.GetComponent<MeshFilter>().mesh;
                MeshRenderer renderer = culledGO.AddComponent<MeshRenderer>();
                renderer.materials = MainRenderer.materials;
            }
            else
            {
                culledVisuals.gameObject.SetActive(true);
            }
        }
    }
}

using System;
using GameDevTV.RTS.Behavior;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Audio;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Utilities;
using Mirror;
using Unity.Behavior;
using UnityEngine;
namespace GameDevTV.RTS.Units
{
    public class Worker : AbstractUnit, IBuildingBuilder, ITransportable
    {
        private readonly WorkerGatherAssignmentLock gatherAssignmentLock = new();
        const float GatherAudioCooldownSeconds = 0.55f;
        float lastGatherAudioTime = -999f;

        public bool IsBuilding => graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> command) && command.Value == UnitCommands.BuildBuilding;

        /// <summary>
        /// Mục tiêu: AI không gán gather/build macro khác khi worker đang trong luồng xây (Behavior Graph).
        /// Cách hoạt động: Command BuildBuilding hoặc site đang Building/Paused trên blackboard.
        /// </summary>
        public bool IsCommittedToConstructionWork =>
            IsBuilding || TryGetActiveConstructionSite(out _);

        /// <summary>
        /// Mục tiêu: Command Gather nhưng mỏ hết/bị phá — BT kẹt, AI không gán lệnh được.
        /// Cách hoạt động: Gather + không HasSupplies + không còn node Amount &gt; 0 trên blackboard.
        /// </summary>
        public bool HasStaleGatherCommand =>
            IsGathering
            && !HasSupplies
            && !TryGetCommittedGatherSupply(out _);

        /// <summary>
        /// Mục tiêu: Cho AI biết worker đang gather/return thật — không tính Gather “mồ chết”.
        /// Cách hoạt động: ReturnSupplies; hoặc Gather kèm mỏ còn tài nguyên / đang mang hàng.
        /// </summary>
        public bool IsGatheringOrReturning =>
            graphAgent != null
            && graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> commandVariable)
            && (commandVariable.Value == UnitCommands.ReturnSupplies
                || (commandVariable.Value == UnitCommands.Gather
                    && (HasSupplies || TryGetCommittedGatherSupply(out _))));

        /// <summary>
        /// Mục tiêu: Chỉ đang khai thác mỏ (không phải đang về kho).
        /// Cách hoạt động: Blackboard Command == Gather.
        /// </summary>
        public bool IsGathering =>
            graphAgent != null
            && graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> commandVariable)
            && commandVariable.Value == UnitCommands.Gather;

        /// <summary>
        /// Mục tiêu: Node gather BT đang dùng (Gather Sub Graph) — kể cả khi Command là Move/Return.
        /// Cách hoạt động: Đọc blackboard Supply, không thì TargetGameObject → <see cref="GatherableSupply"/>.
        /// </summary>
        public bool TryGetCommittedGatherSupply(out GatherableSupply supply)
        {
            supply = null;
            if (graphAgent == null)
            {
                return false;
            }

            if (graphAgent.GetVariable("Supply", out BlackboardVariable<GatherableSupply> supplyVariable)
                && supplyVariable.Value != null
                && supplyVariable.Value.Amount > 0)
            {
                supply = supplyVariable.Value;
                return true;
            }

            if (graphAgent.GetVariable("TargetGameObject", out BlackboardVariable<GameObject> targetVariable)
                && targetVariable.Value != null)
            {
                GatherableSupply fromTarget = targetVariable.Value.GetComponent<GatherableSupply>();
                if (fromTarget == null)
                {
                    fromTarget = targetVariable.Value.GetComponentInChildren<GatherableSupply>();
                }

                if (fromTarget != null && fromTarget.Amount > 0)
                {
                    supply = fromTarget;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: Worker đang trong chu kỳ gather BT (đào / mang tài nguyên / về kho / đi lại mỏ).
        /// Cách hoạt động: HasSupplies, Command gather/return, hoặc Supply/TargetGameObject còn node hợp lệ.
        /// </summary>
        public bool IsInGatherWorkCycle =>
            HasSupplies
            || (graphAgent != null
                && graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> commandVariable)
                && commandVariable.Value == UnitCommands.ReturnSupplies)
            || TryGetCommittedGatherSupply(out _);

        /// <summary>
        /// Mục tiêu: AI/planner có nên gửi Gather tới node này (không phụ thuộc blackboard Supply còn hay bị xóa).
        /// Cách hoạt động: Dùng <see cref="WorkerGatherAssignmentLock"/> — false nếu đã khóa cùng gameObjectId.
        /// </summary>
        public bool ShouldIssueGatherTo(GatherableSupply candidate) => gatherAssignmentLock.ShouldIssueGatherTo(candidate);

        /// <summary>
        /// Mục tiêu: AI có gửi Gather trùng node worker đang khai thác trong BT hay không.
        /// Cách hoạt động: Khóa gather hoặc blackboard Supply/Target cùng node.
        /// </summary>
        public bool IsTargetingSameGatherSupply(GatherableSupply candidate)
        {
            if (candidate == null || candidate.Amount <= 0)
            {
                return false;
            }

            gatherAssignmentLock.RefreshStaleLock();
            if (gatherAssignmentLock.HasLock
                && !gatherAssignmentLock.ShouldIssueGatherTo(candidate))
            {
                return true;
            }

            if (!TryGetCommittedGatherSupply(out GatherableSupply committed))
            {
                return false;
            }

            return AreSameGatherNode(committed, candidate);
        }

        /// <summary>
        /// Mục tiêu: Hai tham chiếu có trỏ cùng một node mỏ trên scene hay không.
        /// Cách hoạt động: ReferenceEquals hoặc cùng gameObject (collider con / BT đổi reference).
        /// </summary>
        public static bool AreSameGatherNode(GatherableSupply a, GatherableSupply b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            return a == b || a.gameObject == b.gameObject;
        }
        public bool HasSupplies
        {
            get
            {
                if (graphAgent != null && graphAgent.GetVariable("SupplyAmountHeld", out BlackboardVariable<int> heldVariable))
                {
                    return heldVariable.Value > 0;
                }

                return false;
            }
        }
        public int TransportCapacityUsage => unitSO.TransportConfig.GetTransportCapacityUsage();
        [SerializeField] private BaseCommand CancelBuildingCommand;

        protected override void Start()
        {
            base.Start();
            if (graphAgent.GetVariable("GatherSuppliesEvent", out BlackboardVariable<GatherSuppliesEventChannel> eventChannelVariable))
            {
                eventChannelVariable.Value.Event += HandleGatherSupplies;
            }
            if (graphAgent.GetVariable("BuildingEventChannel", out BlackboardVariable<BuildingEventChannel> buildingEventChannelVariable))
            {
                buildingEventChannelVariable.Value.Event += HandleBuildingEvent;
            }

            Bus<SupplyDepletedEvent>.OnEvent[Owner] += HandleSupplyDepleted;
        }

        protected override void OnDestroy()
        {
            Bus<SupplyDepletedEvent>.OnEvent[Owner] -= HandleSupplyDepleted;
            base.OnDestroy();
        }

        /// <summary>
        /// Mục tiêu: Thoát gather kẹt khi mỏ hết — reset blackboard + Stop để AI gán mỏ mới.
        /// Cách hoạt động: HasStaleGatherCommand → InterruptGatherWorkCycle + Command Stop.
        /// </summary>
        public void RecoverFromStaleGatherStateIfNeeded()
        {
            gatherAssignmentLock.RefreshStaleLock();
            if (!HasStaleGatherCommand)
            {
                return;
            }

            Stop();
        }

        public void LoadInto(ITransporter transporter)
        {
            ClearGatherAssignmentLock();
            PauseActiveConstructionIfNeeded();
            MoveTo(transporter.Transform);
            transporter.Load(this);
        }

        /// <summary>
        /// Mục tiêu: Bắt đầu/duy trì chu kỳ gather BT trên một node — gọi lại cùng node là no-op.
        /// Cách hoạt động: Khóa gameObjectId; chỉ cập nhật blackboard khi đổi mỏ hoặc lần đầu.
        /// </summary>
        public void Gather(GatherableSupply supply)
        {
            if (supply == null)
            {
                return;
            }

            gatherAssignmentLock.RefreshStaleLock();
            if (!gatherAssignmentLock.ShouldIssueGatherTo(supply))
            {
                return;
            }

            PauseActiveConstructionIfNeeded();
            DisposeMovementDestinationCursor();
            gatherAssignmentLock.Assign(supply);
            SyncGatherBlackboard(supply);
            graphAgent.SetVariableValue("Command", UnitCommands.Gather);
        }

        /// <summary>
        /// Mục tiêu: Đồng bộ blackboard gather (Supply + SupplySO + GatherableSupplies) — tránh return/deposit sai loại.
        /// Cách hoạt động: Gán khi đổi lệnh Gather; không poll trong Update (theo kinh nghiệm Petra).
        /// </summary>
        private void SyncGatherBlackboard(GatherableSupply supply)
        {
            if (graphAgent == null || supply == null)
            {
                return;
            }

            graphAgent.SetVariableValue("Supply", supply);
            graphAgent.SetVariableValue("TargetGameObject", supply.gameObject);
            graphAgent.SetVariableValue("GatherableSupplies", supply);

            if (supply.Supply != null)
            {
                graphAgent.SetVariableValue("SupplySO", supply.Supply);
            }
        }

        /// <summary>
        /// Mục tiêu: Hủy khóa gather khi lệnh macro khác (Stop, build, attack…).
        /// Cách hoạt động: Reset <see cref="WorkerGatherAssignmentLock"/>.
        /// </summary>
        public void ClearGatherAssignmentLock() => gatherAssignmentLock.Clear();

        /// <summary>
        /// Mục tiêu: Client MP — blackboard Gather không replicate từ server; mirror presentation local.
        /// Cách hoạt động: Clear lock, gán Supply/Target, Command = Gather (không chạy lại server logic).
        /// </summary>
        public void MirrorGatherPresentation(GatherableSupply supply)
        {
            if (supply == null || supply.Amount <= 0 || graphAgent == null)
            {
                return;
            }

            gatherAssignmentLock.Clear();
            gatherAssignmentLock.Assign(supply);
            SyncGatherBlackboard(supply);
            graphAgent.SetVariableValue("Command", UnitCommands.Gather);
        }

        /// <summary>
        /// Mục tiêu: Client MP (P2) thấy worker di chuyển/xây sau server xử lý CmdUtsBuildBuilding.
        /// Cách hoạt động: Bật graph tạm, gán blackboard BuildBuilding giống Worker.Build (không trừ supply).
        /// </summary>
        public void MirrorBuildPresentation(BuildingSO building, Vector3 targetLocation)
        {
            if (building == null || graphAgent == null)
            {
                return;
            }

            InterruptGatherWorkCycle();
            DisposeMovementDestinationCursor();

            if (!graphAgent.enabled)
            {
                graphAgent.enabled = true;
            }

            GameObject ghostMarker = new GameObject("BuildPlacementMarker");
            ghostMarker.transform.SetPositionAndRotation(targetLocation, Quaternion.identity);

            graphAgent.SetVariableValue("BuildingSO", building);
            graphAgent.SetVariableValue("TargetLocation", targetLocation);
            graphAgent.SetVariableValue("Ghost", ghostMarker);
            graphAgent.SetVariableValue<BaseBuilding>("BuildingUnderConstruction", null);
            graphAgent.SetVariableValue("Command", UnitCommands.BuildBuilding);
            SetCommandOverrides(new BaseCommand[] { CancelBuildingCommand });
        }

        /// <summary>
        /// Mục tiêu: Client MP biết chính xác nhà nào đang xây (netId từ server).
        /// Cách hoạt động: Gán blackboard BuildingUnderConstruction để BuildBuildingAction client theo dõi Completed.
        /// </summary>
        public void LinkPresentationBuildingUnderConstruction(BaseBuilding building)
        {
            if (building == null || graphAgent == null)
            {
                return;
            }

            graphAgent.SetVariableValue<BaseBuilding>("BuildingUnderConstruction", building);
        }

        /// <summary>
        /// Mục tiêu: Ngắt hẳn gather BT (blackboard + animation) trước Stop/Build — tránh vừa trừ tài nguyên vừa đào.
        /// Cách hoạt động: Xóa Supply/GatherableSupplies/Target; reset SupplyAmountHeld; tắt isEngaging.
        /// </summary>
        public void InterruptGatherWorkCycle()
        {
            ClearGatherAssignmentLock();

            if (graphAgent == null)
            {
                return;
            }

            graphAgent.SetVariableValue<GatherableSupply>("Supply", null);
            graphAgent.SetVariableValue<GameObject>("TargetGameObject", null);
            graphAgent.SetVariableValue<GatherableSupply>("GatherableSupplies", null);
            graphAgent.SetVariableValue("SupplyAmountHeld", 0);

            if (TryGetComponent(out Animator animator))
            {
                animator.SetBool(AnimationConstants.IS_ENGAGING, false);
            }
        }

        /// <summary>
        /// Mục tiêu: Sau khi xây xong/hủy, trả worker về trạng thái planner có thể gán gather/build lại.
        /// Cách hoạt động: InterruptGatherWorkCycle + xóa blackboard build; Command = Stop.
        /// </summary>
        public void ReleasePlannerControlAfterConstructionEnded()
        {
            InterruptGatherWorkCycle();
            DisposeMovementDestinationCursor();

            if (graphAgent == null)
            {
                return;
            }

            graphAgent.SetVariableValue<GameObject>("Ghost", null);
            graphAgent.SetVariableValue<BaseBuilding>("BuildingUnderConstruction", null);
            graphAgent.SetVariableValue<BuildingSO>("BuildingSO", null);
            graphAgent.SetVariableValue("TargetLocation", Vector3.zero);
            SetCommandOverrides(null);
            graphAgent.SetVariableValue("Command", UnitCommands.Stop);
            GameDevTV.RTS.AI.AIConstructionAssignment.ReleaseBuilderIfMatches(GetInstanceID());
            GameDevTV.RTS.AI.AIInfraBuildOrderTracker.ClearOrdersForWorker(Owner, GetInstanceID());
        }

        public void ReturnSupplies(GameObject commandPost)
        {
            PauseActiveConstructionIfNeeded();
            DisposeMovementDestinationCursor();
            graphAgent.SetVariableValue("CommandPost", commandPost);
            if (commandPost != null)
            {
                Vector3 approach = GameDevTV.RTS.Utilities.SupplyDepositApproachUtility.ResolveApproachPosition(
                    transform.position,
                    commandPost,
                    GetInstanceID());
                graphAgent.SetVariableValue("TargetLocation", approach);
            }

            graphAgent.SetVariableValue("Command", UnitCommands.ReturnSupplies);
        }

        public override void MoveTo(Vector3 position)
        {
            PauseActiveConstructionIfNeeded();
            base.MoveTo(position);
        }

        public override void MoveTo(Transform transform)
        {
            PauseActiveConstructionIfNeeded();
            base.MoveTo(transform);
        }

        public override void Attack(IDamageable damageable)
        {
            InterruptGatherWorkCycle();
            PauseActiveConstructionIfNeeded();
            base.Attack(damageable);
        }

        public override void Attack(Vector3 location)
        {
            InterruptGatherWorkCycle();
            PauseActiveConstructionIfNeeded();
            base.Attack(location);
        }

        public override void Stop()
        {
            InterruptGatherWorkCycle();
            PauseActiveConstructionIfNeeded();
            base.Stop();
        }

        public GameObject Build(BuildingSO building, Vector3 targetLocation)
        {
            if (RtsNetplaySession.IsNetworkMatch && !NetworkServer.active)
            {
                return null;
            }

            DisposeMovementDestinationCursor();
            if (building.Prefab != null && !building.Prefab.TryGetComponent(out BaseBuilding _))
            {
                Debug.LogError($"Missing BaseBuilding on Prefab for BuildingSO \"{building.name}\"! Cannot build!");
                return null;
            }

            GameObject ghostMarker = new GameObject("BuildPlacementMarker");
            ghostMarker.transform.SetPositionAndRotation(targetLocation, Quaternion.identity);

            graphAgent.SetVariableValue("BuildingSO", building);
            graphAgent.SetVariableValue("TargetLocation", targetLocation);
            graphAgent.SetVariableValue("Ghost", ghostMarker);
            graphAgent.SetVariableValue<BaseBuilding>("BuildingUnderConstruction", null);
            graphAgent.SetVariableValue("Command", UnitCommands.BuildBuilding);

            SetCommandOverrides(new BaseCommand[] { CancelBuildingCommand });
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -building.Cost.Stone, building.Cost.StoneSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -building.Cost.Wood, building.Cost.WoodSO));
            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, -building.Cost.Food, building.Cost.FoodSO));

            return null;
        }

        public void ResumeBuilding(BaseBuilding building)
        {
            if (building == null || !building.CanResumeConstruction())
            {
                return;
            }

            ClearGatherAssignmentLock();
            PauseActiveConstructionIfNeeded();

            // Không StartBuilding ở đây — graph (BuildBuildingAction) sẽ gọi khi vào nhánh xây; tránh ArrivedAt/BuildingIsInProgress trên graph chặn resume.
            DisposeMovementDestinationCursor();
            if (Agent != null)
            {
                Agent.enabled = true;
            }

            graphAgent.SetVariableValue("TargetLocation", building.transform.position);
            graphAgent.SetVariableValue("BuildingUnderConstruction", building);
            graphAgent.SetVariableValue("BuildingSO", building.BuildingSO);
            graphAgent.SetVariableValue<GameObject>("Ghost", null);
            graphAgent.SetVariableValue("Command", UnitCommands.BuildBuilding);
            SetCommandOverrides(new BaseCommand[] { CancelBuildingCommand });
        }

        /// <summary>
        /// Mục tiêu: Khi worker nhận lệnh khác giữa chừng xây, tạm dừng công trình và bật lại NavMesh (không Destroy nhà).
        /// Cách hoạt động: Nếu blackboard có BuildingUnderConstruction đang Building thì gọi Pause trên site, clear ref, bật agent.
        /// </summary>
        private void PauseActiveConstructionIfNeeded()
        {
            if (!graphAgent.GetVariable("BuildingUnderConstruction", out BlackboardVariable<BaseBuilding> siteVariable)
                || siteVariable.Value == null)
            {
                return;
            }

            BaseBuilding site = siteVariable.Value;
            if (site != null && site.Progress.State == BuildingProgress.BuildingState.Building)
            {
                site.PauseConstructionAndReleaseBuilder();
            }

            if (Agent != null)
            {
                Agent.enabled = true;
            }
        }

        /// <summary>
        /// Mục tiêu: Worker rời chế độ build (Cancel trên UI) mà không phá công trình đang dở.
        /// Cách hoạt động: Xóa marker Ghost nếu còn; pause site qua PauseActiveConstructionIfNeeded; Stop và bỏ override.
        /// </summary>
        public void CancelBuilding()
        {
            GameDevTV.RTS.AI.AIInfraBuildOrderTracker.ClearOrdersForWorker(Owner, GetInstanceID());
            InterruptGatherWorkCycle();
            if (graphAgent.GetVariable("Ghost", out BlackboardVariable<GameObject> ghostVariable)
                && ghostVariable.Value != null)
            {
                Destroy(ghostVariable.Value);
                graphAgent.SetVariableValue<GameObject>("Ghost", null);
            }

            PauseActiveConstructionIfNeeded();

            SetCommandOverrides(Array.Empty<BaseCommand>());
            Stop();
        }

        public override void Deselect()
        {
            if (decalProjector != null)
            {
                decalProjector.gameObject.SetActive(false);
            }

            IsSelected = false;
            if (!IsBuilding)
            {
                SetCommandOverrides(null);
            }

            Bus<UnitDeselectedEvent>.Raise(Owner, new UnitDeselectedEvent(this));
        }

        private void HandleSupplyDepleted(SupplyDepletedEvent evt)
        {
            if (evt.Supply == null || !IsTargetingDepletedSupply(evt.Supply))
            {
                return;
            }

            RecoverFromStaleGatherStateIfNeeded();
        }

        /// <summary>
        /// Mục tiêu: Worker đang gather đúng node vừa bị Destroy/Amount=0.
        /// </summary>
        private bool IsTargetingDepletedSupply(GatherableSupply depleted)
        {
            if (depleted == null)
            {
                return false;
            }

            int depletedId = depleted.gameObject.GetInstanceID();
            if (graphAgent != null
                && graphAgent.GetVariable("Supply", out BlackboardVariable<GatherableSupply> supplyVariable)
                && supplyVariable.Value != null
                && supplyVariable.Value.gameObject.GetInstanceID() == depletedId)
            {
                return true;
            }

            if (graphAgent != null
                && graphAgent.GetVariable("TargetGameObject", out BlackboardVariable<GameObject> targetVariable)
                && targetVariable.Value != null
                && targetVariable.Value.GetInstanceID() == depletedId)
            {
                return true;
            }

            return false;
        }

        private void HandleGatherSupplies(GameObject self, int amount, SupplySO supply)
        {
            if (supply == null)
            {
                return;
            }

            TryPlayGatherAudio(self, amount, supply);

            if (graphAgent != null)
            {
                graphAgent.SetVariableValue("SupplySO", supply);
            }

            Bus<SupplyEvent>.Raise(Owner, new SupplyEvent(Owner, amount, supply));
        }

        void TryPlayGatherAudio(GameObject self, int amount, SupplySO supply)
        {
            if (amount <= 0 || self == null || !LocalHumanOwnerAccess.IsLocalOwner(Owner))
            {
                return;
            }

            if (Time.unscaledTime - lastGatherAudioTime < GatherAudioCooldownSeconds)
            {
                return;
            }

            AudioCueId cue = SupplyGatherAudioUtility.ResolveGatherCue(supply);
            if (cue == AudioCueId.None)
            {
                return;
            }

            lastGatherAudioTime = Time.unscaledTime;
            AudioAccess.TryPlay3D(cue, self.transform.position);
        }

        /// <summary>
        /// Mục tiêu: AI biết worker đang nhắm xây loại nhà nào (đi tới site hoặc đang thi công).
        /// Cách hoạt động: Ưu tiên BuildingUnderConstruction; không có thì Command BuildBuilding + BuildingSO trên blackboard.
        /// </summary>
        public bool TryGetCommittedBuildBuildingName(out string displayName)
        {
            displayName = null;
            if (graphAgent == null)
            {
                return false;
            }

            if (TryGetActiveConstructionSite(out BaseBuilding site)
                && site?.BuildingSO != null)
            {
                displayName = site.BuildingSO.Name;
                return true;
            }

            if (!graphAgent.GetVariable("Command", out BlackboardVariable<UnitCommands> commandVariable)
                || commandVariable.Value != UnitCommands.BuildBuilding)
            {
                return false;
            }

            if (graphAgent.GetVariable("BuildingSO", out BlackboardVariable<BuildingSO> buildingVariable)
                && buildingVariable.Value != null)
            {
                displayName = buildingVariable.Value.Name;
                return true;
            }

            return false;
        }

        private bool TryGetActiveConstructionSite(out BaseBuilding site)
        {
            site = null;
            if (graphAgent == null
                || !graphAgent.GetVariable("BuildingUnderConstruction", out BlackboardVariable<BaseBuilding> siteVariable)
                || siteVariable.Value == null)
            {
                return false;
            }

            BuildingProgress.BuildingState state = siteVariable.Value.Progress.State;
            if (state != BuildingProgress.BuildingState.Building
                && state != BuildingProgress.BuildingState.Paused)
            {
                return false;
            }

            site = siteVariable.Value;
            return true;
        }

        private void HandleBuildingEvent(GameObject self, BuildingEventType eventType, BaseBuilding building)
        {
            switch(eventType)
            {
                case BuildingEventType.ArrivedAt:
                    SetCommandOverrides(new BaseCommand[] { CancelBuildingCommand });
                    break;
                case BuildingEventType.Begin:
                    SetCommandOverrides(new BaseCommand[] { CancelBuildingCommand });
                    break;
                case BuildingEventType.Cancel:
                case BuildingEventType.Abort:
                case BuildingEventType.Completed:
                    ReleasePlannerControlAfterConstructionEnded();
                    break;
                default:
                    break;
            }
        }
    }
}

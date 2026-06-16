using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.UI.Containers;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.UI
{
    /// <summary>
    /// SRP: Panel lệnh / selection UI — subscribe Bus theo một <see cref="Owner"/> (P1 hoặc P2).
    /// </summary>
    public class RuntimeUI : MonoBehaviour
    {
        [SerializeField] private ActionsUI actionsUI;
        [SerializeField] private BuildingSelectedUI buildingSelectedUI;
        [SerializeField] private UnitIconUI unitIconUI;
        [SerializeField] private SingleUnitSelectedUI singleUnitSelectedUI;
        [SerializeField] private MultiUnitSelectionUI multiUnitSelectionUI;
        [SerializeField] private UnitTransportUI unitTransportUI;

        [SerializeField] Owner eventBusOwner = Owner.Invalid;

        readonly HashSet<AbstractCommandable> selectedUnits = new(12);
        bool busSubscribed;

        static readonly List<AbstractCommandable> ResyncSelectionBuffer = new(12);

        void Awake()
        {
            if (eventBusOwner == Owner.Invalid)
            {
                eventBusOwner = InferBusOwnerFromHierarchy();
            }
        }

        void OnEnable()
        {
            LocalHumanOwnerService.LocalOwnerChanged += OnLocalOwnerChanged;
            TryConfigureForLocalHuman();
        }

        void Start()
        {
            TryConfigureForLocalHuman();
        }

        void OnDisable()
        {
            LocalHumanOwnerService.LocalOwnerChanged -= OnLocalOwnerChanged;
            UnsubscribeBus();
        }

        void OnDestroy()
        {
            LocalHumanOwnerService.LocalOwnerChanged -= OnLocalOwnerChanged;
            UnsubscribeBus();
        }

        void OnLocalOwnerChanged(Owner owner) => TryConfigureForLocalHuman();

        /// <summary>
        /// Mục tiêu: MP — chỉ rig HUD đang active + đúng human local mới subscribe Bus.
        /// Cách hoạt động: So khớp LocalOwner với MpPlayerPresentationRig.PresentationOwner (nếu có).
        /// </summary>
        void TryConfigureForLocalHuman()
        {
            Owner local = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            if (!HumanFogVisionUtility.IsHumanPlayer(local))
            {
                return;
            }

            Owner branchOwner = MpHudBranchResolver.ResolveFor(transform);
            if (branchOwner != local)
            {
                return;
            }

            if (IsConfiguredForOwner(local))
            {
                return;
            }

            ConfigureBusOwner(local);
        }

        /// <summary>Mục tiêu: Rig inactive — gỡ subscription Bus (tránh P1/P2 cùng nghe).</summary>
        public void ReleaseBusSubscription()
        {
            UnsubscribeBus();
            selectedUnits.Clear();
        }

        /// <summary>Mục tiêu: Coordinator refresh action bar sau supply snapshot MP.</summary>
        public bool IsConfiguredForOwner(Owner owner) =>
            busSubscribed && eventBusOwner == owner && isActiveAndEnabled;

        /// <summary>Mục tiêu: Refresh panel lệnh khi tài nguyên đổi qua SyncVar (không có SupplyEvent client).</summary>
        public void RefreshSelectionPresentation()
        {
            TryResyncSelectionFromPlayerInput();
            RefreshUI();
        }

        /// <summary>
        /// Mục tiêu: MP client — PlayerInput vẫn giữ selection nhưng RuntimeUI HashSet rỗng sau ConfigureBusOwner/refresh.
        /// Cách hoạt động: Sao chép commandable đang chọn từ PlayerInput trước khi RefreshUI.
        /// </summary>
        void TryResyncSelectionFromPlayerInput()
        {
            if (!RtsNetplaySession.IsNetworkMatch)
            {
                return;
            }

            PlayerInput input = Object.FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Include);
            if (input == null)
            {
                return;
            }

            ResyncSelectionBuffer.Clear();
            input.CollectSelectedCommandablesForUi(ResyncSelectionBuffer);
            if (ResyncSelectionBuffer.Count == 0)
            {
                return;
            }

            selectedUnits.Clear();
            for (int i = 0; i < ResyncSelectionBuffer.Count; i++)
            {
                selectedUnits.Add(ResyncSelectionBuffer[i]);
            }
        }

        /// <summary>
        /// Mục tiêu: Client MP — progress bar / queue nhà đang chọn cập nhật sau network sync.
        /// </summary>
        public void RefreshBuildingIfSelected(BaseBuilding building)
        {
            if (building == null || buildingSelectedUI == null || !selectedUnits.Contains(building))
            {
                return;
            }

            buildingSelectedUI.EnableFor(building);
        }

        /// <summary>
        /// Mục tiêu: MP — HUD P2 nghe Bus Player2; P1 nghe Player1.
        /// Cách hoạt động: Hủy owner cũ, đăng ký owner mới, reset selection.
        /// </summary>
        public void ConfigureBusOwner(Owner owner)
        {
            if (!HumanFogVisionUtility.IsHumanPlayer(owner))
            {
                return;
            }

            if (!busSubscribed || eventBusOwner != owner)
            {
                UnsubscribeBus();
                eventBusOwner = owner;
                SubscribeBus(owner);
                selectedUnits.Clear();

                if (isActiveAndEnabled)
                {
                    DisableAllContainers();
                }
            }
        }

        Owner InferBusOwnerFromHierarchy()
        {
            Transform t = transform;
            while (t != null)
            {
                if (t.name.Contains("(1)"))
                {
                    return Owner.Player2;
                }

                t = t.parent;
            }

            return Owner.Player1;
        }

        void SubscribeBus(Owner owner)
        {
            Bus<UnitSelectedEvent>.OnEvent[owner] += HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[owner] += HandleUnitDeselected;
            Bus<UnitDeathEvent>.OnEvent[owner] += HandleUnitDeath;
            Bus<SupplyEvent>.OnEvent[owner] += HandleSupplyChange;
            Bus<UnitLoadEvent>.OnEvent[owner] += HandleLoadUnit;
            Bus<UnitUnloadEvent>.OnEvent[owner] += HandleUnloadUnit;
            Bus<BuildingSpawnEvent>.OnEvent[owner] += HandleBuildingSpawn;
            Bus<UpgradeResearchedEvent>.OnEvent[owner] += HandleUpgradeResearched;
            Bus<BuildingDeathEvent>.OnEvent[owner] += HandleBuildingDeath;
            busSubscribed = true;
        }

        void UnsubscribeBus()
        {
            if (!busSubscribed || !HumanFogVisionUtility.IsHumanPlayer(eventBusOwner))
            {
                return;
            }

            Owner owner = eventBusOwner;
            Bus<UnitSelectedEvent>.OnEvent[owner] -= HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[owner] -= HandleUnitDeselected;
            Bus<UnitDeathEvent>.OnEvent[owner] -= HandleUnitDeath;
            Bus<SupplyEvent>.OnEvent[owner] -= HandleSupplyChange;
            Bus<UnitLoadEvent>.OnEvent[owner] -= HandleLoadUnit;
            Bus<UnitUnloadEvent>.OnEvent[owner] -= HandleUnloadUnit;
            Bus<BuildingSpawnEvent>.OnEvent[owner] -= HandleBuildingSpawn;
            Bus<UpgradeResearchedEvent>.OnEvent[owner] -= HandleUpgradeResearched;
            Bus<BuildingDeathEvent>.OnEvent[owner] -= HandleBuildingDeath;
            busSubscribed = false;
        }

        void HandleUnitSelected(UnitSelectedEvent evt)
        {
            if (evt.Unit is AbstractCommandable commandable)
            {
                selectedUnits.Add(commandable);
                RefreshUI();
            }
        }

        void HandleUnitDeath(UnitDeathEvent evt)
        {
            selectedUnits.Remove(evt.Unit);
            RefreshUI();
        }

        void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            selectedUnits.Remove(evt.Building);
            RefreshUI();
        }

        void HandleUpgradeResearched(UpgradeResearchedEvent args)
        {
            RefreshUI();
        }

        void HandleBuildingSpawn(BuildingSpawnEvent args)
        {
            if (selectedUnits.Count == 1 && selectedUnits.First() is Worker)
            {
                actionsUI.EnableFor(selectedUnits);
            }
        }

        void HandleLoadUnit(UnitLoadEvent evt)
        {
            if (selectedUnits.Count == 1 && selectedUnits.First() is ITransporter)
            {
                RefreshUI();
            }
            else if (evt.Unit is AbstractCommandable commandable && selectedUnits.Contains(commandable))
            {
                commandable.Deselect();
            }
        }

        void HandleUnloadUnit(UnitUnloadEvent evt)
        {
            if (selectedUnits.Count == 1 && selectedUnits.First() is ITransporter)
            {
                RefreshUI();
            }
        }

        void HandleUnitDeselected(UnitDeselectedEvent evt)
        {
            if (evt.Unit is AbstractCommandable commandable)
            {
                selectedUnits.Remove(commandable);
                RefreshUI();
            }
        }

        void RefreshUI()
        {
            if (selectedUnits.Count == 0 && RtsNetplaySession.IsNetworkMatch)
            {
                TryResyncSelectionFromPlayerInput();
            }

            if (selectedUnits.Count > 0)
            {
                actionsUI.EnableFor(selectedUnits);

                if (selectedUnits.Count == 1)
                {
                    ResolveSingleUnitSelectedUI();
                }
                else
                {
                    unitIconUI.Disable();
                    singleUnitSelectedUI.Disable();
                    buildingSelectedUI.Disable();
                    unitTransportUI.Disable();
                    multiUnitSelectionUI?.EnableFor(selectedUnits);
                }
            }
            else
            {
                DisableAllContainers();
            }
        }

        void DisableAllContainers()
        {
            if (actionsUI != null)
            {
                actionsUI.Disable();
            }

            if (buildingSelectedUI != null)
            {
                buildingSelectedUI.Disable();
            }

            if (unitIconUI != null)
            {
                unitIconUI.Disable();
            }

            if (singleUnitSelectedUI != null)
            {
                singleUnitSelectedUI.Disable();
            }

            multiUnitSelectionUI?.Disable();

            if (unitTransportUI != null)
            {
                unitTransportUI.Disable();
            }
        }

        void ResolveSingleUnitSelectedUI()
        {
            multiUnitSelectionUI?.Disable();
            AbstractCommandable commandable = selectedUnits.First();
            unitIconUI.EnableFor(commandable);

            if (commandable is BaseBuilding building)
            {
                singleUnitSelectedUI.Disable();
                unitTransportUI.Disable();
                buildingSelectedUI.EnableFor(building);
            }
            else if (commandable is ITransporter transporter && transporter.UsedCapacity > 0)
            {
                unitTransportUI.EnableFor(transporter);
                buildingSelectedUI.Disable();
                singleUnitSelectedUI.Disable();
            }
            else
            {
                buildingSelectedUI.Disable();
                unitTransportUI.Disable();
                singleUnitSelectedUI.EnableFor(commandable);
            }
        }

        void HandleSupplyChange(SupplyEvent evt)
        {
            actionsUI.EnableFor(selectedUnits);
        }
    }
}

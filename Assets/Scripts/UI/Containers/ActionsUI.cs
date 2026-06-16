using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.UI.Components;
using GameDevTV.RTS.UI;
using UnityEngine;
using UnityEngine.Events;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using System;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;

namespace GameDevTV.RTS.UI.Containers
{
    public class ActionsUI : MonoBehaviour, IUIElement<HashSet<AbstractCommandable>>
    {
        [SerializeField] private UIActionButton[] actionButtons;

        private HashSet<BaseBuilding> selectedBuildings = new();
        private BaseCommand pendingActiveCommand;
        private Owner subscribedBusOwner = Owner.Invalid;

        void Awake()
        {
            if (actionButtons == null)
            {
                return;
            }

            for (int i = 0; i < actionButtons.Length; i++)
            {
                if (actionButtons[i] != null)
                {
                    actionButtons[i].SetDisplaySlot(i);
                }
            }
        }

        void OnEnable()
        {
            LocalHumanOwnerService.LocalOwnerChanged += OnLocalOwnerChanged;
            SubscribeCommandPendingBus();
        }

        void OnDisable()
        {
            LocalHumanOwnerService.LocalOwnerChanged -= OnLocalOwnerChanged;
            UnsubscribeCommandPendingBus();
        }

        void OnLocalOwnerChanged(Owner owner) => SubscribeCommandPendingBus();

        void SubscribeCommandPendingBus()
        {
            Owner owner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            if (subscribedBusOwner == owner)
            {
                return;
            }

            UnsubscribeCommandPendingBus();
            subscribedBusOwner = owner;
            Bus<ActiveCommandChangedEvent>.OnEvent[owner] += HandleActiveCommandChanged;
        }

        void UnsubscribeCommandPendingBus()
        {
            if (subscribedBusOwner == Owner.Invalid)
            {
                return;
            }

            Bus<ActiveCommandChangedEvent>.OnEvent[subscribedBusOwner] -= HandleActiveCommandChanged;
            subscribedBusOwner = Owner.Invalid;
        }

        void HandleActiveCommandChanged(ActiveCommandChangedEvent evt)
        {
            pendingActiveCommand = evt.Command;
            RefreshPendingHighlights();
        }

        void RefreshPendingHighlights()
        {
            if (actionButtons == null)
            {
                return;
            }

            for (int i = 0; i < actionButtons.Length; i++)
            {
                if (actionButtons[i] != null)
                {
                    actionButtons[i].RefreshPendingHighlight(pendingActiveCommand);
                }
            }
        }

        public void EnableFor(HashSet<AbstractCommandable> selectedUnits)
        {
            if (selectedUnits == null || selectedUnits.Count == 0)
            {
                Disable();
                return;
            }

            RefreshButtons(selectedUnits);

            foreach(BaseBuilding building in selectedBuildings)
            {
                building.OnQueueUpdated -= OnBuildingQueueUpdated;
            }

            selectedBuildings = selectedUnits
                .Where(selectedUnit => selectedUnit is BaseBuilding)
                .Cast<BaseBuilding>()
                .ToHashSet();
            
            foreach(BaseBuilding building in selectedBuildings)
            {
                building.OnQueueUpdated += OnBuildingQueueUpdated;
            }
        }

        public void Disable()
        {
            if (actionButtons == null)
            {
                return;
            }

            foreach (UIActionButton button in actionButtons)
            {
                if (button != null)
                {
                    button.Disable();
                }
            }

            foreach (BaseBuilding building in selectedBuildings)
            {
                building.OnQueueUpdated -= OnBuildingQueueUpdated;
            }
            selectedBuildings.Clear();
        }

        private void OnBuildingQueueUpdated(UnlockableSO[] unitsInQueue)
        {
            if (selectedBuildings == null || selectedBuildings.Count == 0)
            {
                return;
            }

            RefreshButtons(selectedBuildings.Cast<AbstractCommandable>().ToHashSet());
        }

        private void RefreshButtons(HashSet<AbstractCommandable> selectedUnits)
        {
            if (selectedUnits == null || selectedUnits.Count == 0)
            {
                for (int i = 0; i < actionButtons.Length; i++)
                {
                    actionButtons[i].Disable();
                }

                return;
            }

            for (int i = 0; i < actionButtons.Length; i++)
            {
                if (ActionBarCommandResolver.TryGetCommandForSlot(selectedUnits, i, out BaseCommand actionForSlot))
                {
                    actionButtons[i].EnableFor(actionForSlot, selectedUnits, HandleClick(actionForSlot));
                }
                else
                {
                    actionButtons[i].Disable();
                }
            }

            RefreshPendingHighlights();
        }

        private UnityAction HandleClick(BaseCommand action)
        {
            Owner busOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            return () => Bus<CommandSelectedEvent>.Raise(busOwner, new CommandSelectedEvent(action));
        }
    }
}

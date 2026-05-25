using System.Collections.Generic;
using System.Linq;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.UI.Components;
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

            AbstractCommandable first = selectedUnits.First();
            IEnumerable<BaseCommand> firstCommands = first.AvailableCommands ?? Array.Empty<BaseCommand>();

            Owner busOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            IEnumerable<BaseCommand> availableCommands = firstCommands.Where(action => action.IsAvailable(
                new CommandContext(
                    busOwner,
                    first,
                    new RaycastHit()
                )
            ));

            for(int i = 1; i<selectedUnits.Count; i++)
            {
                AbstractCommandable commandable = selectedUnits.ElementAt(i);
                if (commandable.AvailableCommands != null)
                {
                    availableCommands = availableCommands.Intersect(commandable.AvailableCommands);
                }
            }

            BaseCommand[] slotSource = availableCommands.ToArray();

            for (int i = 0; i < actionButtons.Length; i++)
            {
                BaseCommand actionForSlot = slotSource.Where(action => action.Slot == i).FirstOrDefault();

                if (actionForSlot != null)
                {
                    actionButtons[i].EnableFor(actionForSlot, selectedUnits, HandleClick(actionForSlot));
                }
                else
                {
                    actionButtons[i].Disable();
                }
            }
        }

        private UnityAction HandleClick(BaseCommand action)
        {
            Owner busOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            return () => Bus<CommandSelectedEvent>.Raise(busOwner, new CommandSelectedEvent(action));
        }
    }
}

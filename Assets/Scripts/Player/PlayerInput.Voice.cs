using System.Collections.Generic;
using GameDevTV.RTS.AI;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.UI;
using GameDevTV.RTS.UI.GameEventLog;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using ProjectRTS.SpeechRecognition.Core;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Thực thi CommandId từ voice → selection / arm command / stop / gather.
    /// </summary>
    public partial class PlayerInput
    {
        GameObject cachedWorkerPrefab;
        GameObject cachedWarriorPrefab;
        GameObject cachedArcherPrefab;
        GameObject cachedRockWarriorPrefab;
        SupplySO cachedStoneSupply;
        SupplySO cachedWoodSupply;
        SupplySO cachedFoodSupply;

        /// <summary>
        /// Mục tiêu: Chạy hành động gameplay tương ứng CommandId voice đã khớp dataset.
        /// Cách hoạt động: Resolve VoiceGameplayAction → selection/stop/arm UI command/gather mỏ gần camera.
        /// </summary>
        public bool TryExecuteVoiceCommand(string commandId)
        {
            if (!VoiceCommandIdActionRules.TryResolve(commandId, out VoiceCommandIdActionRules.VoiceGameplayAction action))
            {
                GameEventLog.Post($"[VoiceCmd] Chưa hỗ trợ lệnh: {commandId}", GameEventLogCategory.Warning);
                return false;
            }

            EnsureLocalOwnerBeforeHotkey();

            switch (action.Kind)
            {
                case VoiceCommandIdActionRules.VoiceGameplayActionKind.StopSelection:
                    StopSelectedUnitsFromHotkey();
                    GameEventLog.Post("[VoiceCmd] Dừng đơn vị đang chọn.", GameEventLogCategory.Info);
                    return true;

                case VoiceCommandIdActionRules.VoiceGameplayActionKind.SelectUnits:
                    VoiceSelectUnits(action.UnitArchetype, action.SelectCount);
                    return true;

                case VoiceCommandIdActionRules.VoiceGameplayActionKind.SelectIdleWorkers:
                    VoiceSelectIdleWorkers();
                    return true;

                case VoiceCommandIdActionRules.VoiceGameplayActionKind.SelectAllMilitary:
                    VoiceSelectAllMilitary();
                    return true;

                case VoiceCommandIdActionRules.VoiceGameplayActionKind.ArmWorldTargetCommand:
                    return VoiceTryArmCommand(action.CommandPredicate, requiresWorldClick: true);

                case VoiceCommandIdActionRules.VoiceGameplayActionKind.ActivateUiCommand:
                    return VoiceTryArmOrExecuteCommand(action.CommandPredicate);

                case VoiceCommandIdActionRules.VoiceGameplayActionKind.GatherNearestSupply:
                    return VoiceTryGatherNearestSupply(action.SupplyKind);

                default:
                    return false;
            }
        }

        void VoiceSelectUnits(string archetype, int count)
        {
            if (!TryResolveUnitPrefab(archetype, out GameObject prefab))
            {
                GameEventLog.Post($"[VoiceCmd] Không tìm prefab cho '{archetype}'.", GameEventLogCategory.Warning);
                return;
            }

            List<AbstractUnit> matches = CollectUnitsOfKindOnScreen(prefab);
            if (matches.Count == 0)
            {
                GameEventLog.Post($"[VoiceCmd] Không có unit '{archetype}' trên màn hình.", GameEventLogCategory.Warning);
                return;
            }

            DeselectAllUnits();
            int limit = Mathf.Min(count, matches.Count);
            for (int i = 0; i < limit; i++)
            {
                TrySelectLocalOwned(matches[i]);
            }

            GameEventLog.Post($"[VoiceCmd] Đã chọn {limit} {archetype}.", GameEventLogCategory.Info);
        }

        void VoiceSelectIdleWorkers()
        {
            if (!TryResolveUnitPrefab("worker", out GameObject workerPrefab))
            {
                return;
            }

            List<AbstractUnit> matches = CollectUnitsOfKindOnScreen(workerPrefab);
            DeselectAllUnits();

            int selected = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                if (matches[i] is not Worker worker || !IsVoiceIdleWorker(worker))
                {
                    continue;
                }

                TrySelectLocalOwned(worker);
                selected++;
            }

            GameEventLog.Post(
                selected > 0
                    ? $"[VoiceCmd] Đã chọn {selected} dân rảnh."
                    : "[VoiceCmd] Không có dân rảnh trên màn hình.",
                selected > 0 ? GameEventLogCategory.Info : GameEventLogCategory.Warning);
        }

        void VoiceSelectAllMilitary()
        {
            DeselectAllUnits();
            int selected = 0;

            selected += VoiceSelectAllOfArchetypeOnScreen("warrior");
            selected += VoiceSelectAllOfArchetypeOnScreen("archer");
            selected += VoiceSelectAllOfArchetypeOnScreen("rockwarrior");

            GameEventLog.Post(
                selected > 0
                    ? $"[VoiceCmd] Đã chọn {selected} quân."
                    : "[VoiceCmd] Không có quân trên màn hình.",
                selected > 0 ? GameEventLogCategory.Info : GameEventLogCategory.Warning);
        }

        int VoiceSelectAllOfArchetypeOnScreen(string archetype)
        {
            if (!TryResolveUnitPrefab(archetype, out GameObject prefab))
            {
                return 0;
            }

            List<AbstractUnit> matches = CollectUnitsOfKindOnScreen(prefab);
            for (int i = 0; i < matches.Count; i++)
            {
                TrySelectLocalOwned(matches[i]);
            }

            return matches.Count;
        }

        bool VoiceTryArmCommand(System.Func<BaseCommand, bool> predicate, bool requiresWorldClick)
        {
            if (!VoiceTryFindCommandOnSelection(predicate, out BaseCommand command))
            {
                GameEventLog.Post("[VoiceCmd] Lệnh không khả dụng với đơn vị đang chọn.", GameEventLogCategory.Warning);
                return false;
            }

            if (!requiresWorldClick || !command.RequiresClickToActivate)
            {
                return VoiceTryExecuteImmediateCommand(command);
            }

            Bus<CommandSelectedEvent>.Raise(ResolveLocalOwner(), new CommandSelectedEvent(command));
            GameEventLog.Post($"[VoiceCmd] Chọn điểm trên map cho: {command.Name}.", GameEventLogCategory.Info);
            return true;
        }

        bool VoiceTryArmOrExecuteCommand(System.Func<BaseCommand, bool> predicate)
        {
            if (!VoiceTryFindCommandOnSelection(predicate, out BaseCommand command))
            {
                GameEventLog.Post("[VoiceCmd] Lệnh không khả dụng — chọn nhà/unit phù hợp.", GameEventLogCategory.Warning);
                return false;
            }

            if (command.RequiresClickToActivate)
            {
                Bus<CommandSelectedEvent>.Raise(ResolveLocalOwner(), new CommandSelectedEvent(command));
                GameEventLog.Post($"[VoiceCmd] Chọn vị trí cho: {command.Name}.", GameEventLogCategory.Info);
                return true;
            }

            return VoiceTryExecuteImmediateCommand(command);
        }

        bool VoiceTryExecuteImmediateCommand(BaseCommand command)
        {
            List<AbstractCommandable> commandables = CollectSelectedCommandables();
            if (commandables.Count == 0)
            {
                return false;
            }

            AbstractCommandable[] units = commandables.ToArray();
            if (!ActionBarCommandExecution.TryValidateForExecution(command, units))
            {
                return false;
            }

            bool executed = false;
            for (int i = 0; i < commandables.Count; i++)
            {
                AbstractCommandable commandable = commandables[i];
                CommandContext context = new(commandable, new RaycastHit());
                if (!command.CanHandle(context) || command.IsLocked(context))
                {
                    continue;
                }

                command.Handle(context);
                executed = true;
                if (command.IsSingleUnitCommand)
                {
                    break;
                }
            }

            if (executed)
            {
                GameEventLog.Post($"[VoiceCmd] Đã thực hiện: {command.Name}.", GameEventLogCategory.Info);
            }

            return executed;
        }

        bool VoiceTryGatherNearestSupply(AIEconomySupplyKindClassifier.Kind supplyKind)
        {
            List<AbstractUnit> workers = CollectSelectedAbstractUnits();
            if (workers.Count == 0)
            {
                GameEventLog.Post("[VoiceCmd] Chọn dân trước khi thu tài nguyên.", GameEventLogCategory.Warning);
                return false;
            }

            if (!VoiceTryFindNearestSupply(supplyKind, out GatherableSupply supply, out RaycastHit hit))
            {
                GameEventLog.Post("[VoiceCmd] Không tìm mỏ phù hợp gần camera.", GameEventLogCategory.Warning);
                return false;
            }

            bool any = false;
            for (int i = 0; i < workers.Count; i++)
            {
                if (workers[i] is not Worker)
                {
                    continue;
                }

                if (TryDispatchCommandsToUnits(
                        new List<AbstractUnit> { workers[i] },
                        hit,
                        commandBeingActivated: null,
                        MouseButton.Right))
                {
                    any = true;
                }
            }

            if (any)
            {
                GameEventLog.Post($"[VoiceCmd] Thu {supplyKind} tại mỏ gần nhất.", GameEventLogCategory.Info);
            }

            return any;
        }

        bool VoiceTryFindNearestSupply(
            AIEconomySupplyKindClassifier.Kind supplyKind,
            out GatherableSupply supply,
            out RaycastHit hit)
        {
            supply = null;
            hit = default;
            VoiceEnsureSupplyReferences();

            Vector3 origin = cameraTarget != null ? cameraTarget.position : Vector3.zero;
            float bestSqr = float.MaxValue;
            GatherableSupply[] supplies = FindObjectsByType<GatherableSupply>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < supplies.Length; i++)
            {
                GatherableSupply candidate = supplies[i];
                if (candidate == null
                    || candidate.Amount <= 0
                    || candidate.Supply == null
                    || AIEconomySupplyKindClassifier.Classify(
                        candidate.Supply,
                        cachedStoneSupply,
                        cachedWoodSupply,
                        cachedFoodSupply)
                        != supplyKind)
                {
                    continue;
                }

                float sqr = (candidate.transform.position - origin).sqrMagnitude;
                if (sqr >= bestSqr)
                {
                    continue;
                }

                bestSqr = sqr;
                supply = candidate;
            }

            if (supply == null)
            {
                return false;
            }

            Vector3 point = supply.transform.position;
            RaycastHit[] hits = Physics.RaycastAll(
                point + Vector3.up * 25f,
                Vector3.down,
                50f,
                interactableLayers | floorLayers,
                QueryTriggerInteraction.Collide);

            for (int h = 0; h < hits.Length; h++)
            {
                if (hits[h].collider != null
                    && hits[h].collider.GetComponentInParent<GatherableSupply>() == supply)
                {
                    hit = hits[h];
                    return true;
                }
            }

            Ray cameraRay = camera.ScreenPointToRay(camera.WorldToScreenPoint(point));
            if (GameplayWorldRaycastUtility.TryGetCommandRaycastHit(
                    cameraRay,
                    interactableLayers | floorLayers,
                    out hit)
                && hit.collider != null
                && hit.collider.GetComponentInParent<GatherableSupply>() == supply)
            {
                return true;
            }

            return false;
        }

        bool VoiceTryFindCommandOnSelection(System.Func<BaseCommand, bool> predicate, out BaseCommand command)
        {
            command = null;
            if (predicate == null)
            {
                return false;
            }

            List<AbstractCommandable> commandables = CollectSelectedCommandables();
            if (commandables.Count == 0)
            {
                return false;
            }

            Owner owner = ResolveLocalOwner();
            for (int i = 0; i < commandables.Count; i++)
            {
                AbstractCommandable commandable = commandables[i];
                IEnumerable<BaseCommand> available = commandable.AvailableCommands ?? System.Array.Empty<BaseCommand>();
                foreach (BaseCommand candidate in available)
                {
                    if (candidate == null || !predicate(candidate))
                    {
                        continue;
                    }

                    CommandContext context = new(owner, commandable, new RaycastHit());
                    if (candidate.IsAvailable(context) && !candidate.IsLocked(context))
                    {
                        command = candidate;
                        return true;
                    }
                }

                if (commandable is AbstractUnit unit)
                {
                    List<BaseCommand> flattened = AvailableCommandsResolver.GetFlattened(unit);
                    for (int c = 0; c < flattened.Count; c++)
                    {
                        BaseCommand candidate = flattened[c];
                        if (candidate == null || !predicate(candidate))
                        {
                            continue;
                        }

                        CommandContext context = new(owner, commandable, new RaycastHit());
                        if (candidate.IsAvailable(context) && !candidate.IsLocked(context))
                        {
                            command = candidate;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        bool TryResolveUnitPrefab(string archetype, out GameObject prefab)
        {
            prefab = null;
            if (string.IsNullOrWhiteSpace(archetype))
            {
                return false;
            }

            string key = archetype.Trim().ToLowerInvariant();
            switch (key)
            {
                case "worker":
                    prefab = cachedWorkerPrefab;
                    break;
                case "warrior":
                    prefab = cachedWarriorPrefab;
                    break;
                case "archer":
                    prefab = cachedArcherPrefab;
                    break;
                case "rockwarrior":
                    prefab = cachedRockWarriorPrefab;
                    break;
            }

            if (prefab != null)
            {
                return true;
            }

            prefab = VoiceFindPrefabFromLocalUnits(key);
            switch (key)
            {
                case "worker":
                    cachedWorkerPrefab = prefab;
                    break;
                case "warrior":
                    cachedWarriorPrefab = prefab;
                    break;
                case "archer":
                    cachedArcherPrefab = prefab;
                    break;
                case "rockwarrior":
                    cachedRockWarriorPrefab = prefab;
                    break;
            }

            return prefab != null;
        }

        GameObject VoiceFindPrefabFromLocalUnits(string archetype)
        {
            AbstractUnit[] units = FindObjectsByType<AbstractUnit>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                AbstractUnit unit = units[i];
                if (unit == null || !IsOwnedByLocalPlayer(unit) || unit.UnitSO?.Prefab == null)
                {
                    continue;
                }

                string unitName = RecognizedSpeechPhraseNormalizer.ToDatasetPhraseForm(unit.UnitSO.name);
                if (unitName.Contains(archetype, System.StringComparison.Ordinal))
                {
                    return unit.UnitSO.Prefab;
                }
            }

            return null;
        }

        void VoiceEnsureSupplyReferences()
        {
            if (cachedStoneSupply != null && cachedWoodSupply != null && cachedFoodSupply != null)
            {
                return;
            }

            GatherableSupply[] supplies = FindObjectsByType<GatherableSupply>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < supplies.Length; i++)
            {
                SupplySO so = supplies[i]?.Supply;
                if (so == null)
                {
                    continue;
                }

                switch (AIEconomySupplyKindClassifier.ClassifyByName(so.name))
                {
                    case AIEconomySupplyKindClassifier.Kind.Stone:
                        cachedStoneSupply ??= so;
                        break;
                    case AIEconomySupplyKindClassifier.Kind.Wood:
                        cachedWoodSupply ??= so;
                        break;
                    case AIEconomySupplyKindClassifier.Kind.Food:
                        cachedFoodSupply ??= so;
                        break;
                }
            }
        }

        static bool IsVoiceIdleWorker(Worker worker) =>
            worker != null
            && !worker.IsGatheringOrReturning
            && !worker.IsBuilding
            && !worker.IsCommittedToConstructionWork;
    }
}

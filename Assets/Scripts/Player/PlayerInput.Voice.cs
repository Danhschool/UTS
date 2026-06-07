using System.Collections.Generic;
using GameDevTV.RTS.AI;
using GameDevTV.RTS.Environment;
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
    /// SRP: Thực thi CommandId voice theo spec slot action bar UTS.
    /// </summary>
    public partial class PlayerInput
    {
        const float VoiceAttackScanRadius = 45f;

        GameObject cachedWorkerPrefab;
        GameObject cachedWarriorPrefab;
        GameObject cachedArcherPrefab;
        GameObject cachedRockWarriorPrefab;
        GameObject cachedCivilCentralPrefab;
        GameObject cachedForgePrefab;
        GameObject cachedBarrackPrefab;
        SupplySO cachedStoneSupply;
        SupplySO cachedWoodSupply;
        SupplySO cachedFoodSupply;

        /// <summary>
        /// Mục tiêu: Chạy hành động gameplay sau khi voice khớp CommandId.
        /// Cách hoạt động: Lấy VoiceCommandExecutionPlan → selection / slot UI / gather / attack.
        /// </summary>
        public bool TryExecuteVoiceCommand(string commandId)
        {
            if (!VoiceCommandExecutionPlanCatalog.TryGetPlan(commandId, out VoiceCommandExecutionPlan plan))
            {
                GameEventLog.Post($"[VoiceCmd] Chưa hỗ trợ lệnh: {commandId}", GameEventLogCategory.Warning);
                return false;
            }

            EnsureLocalOwnerBeforeHotkey();

            switch (plan.Kind)
            {
                case VoiceCommandExecutionKind.StopSelection:
                    StopSelectedUnitsFromHotkey();
                    GameEventLog.Post("[VoiceCmd] Dừng đơn vị đang chọn.", GameEventLogCategory.Info);
                    return true;

                case VoiceCommandExecutionKind.ActionBarSlotOnSelection:
                    return VoiceTryPressUiSlot(plan.UiSlot1Based, "di chuyển");

                case VoiceCommandExecutionKind.AttackNearbyHostiles:
                    return VoiceAttackNearbyHostiles();

                case VoiceCommandExecutionKind.SelectAllUnitsOnScreen:
                    return VoiceSelectAllUnitsOnScreen();

                case VoiceCommandExecutionKind.SelectUnitsByArchetype:
                    VoiceSelectUnits(plan.UnitArchetype, plan.SelectCount);
                    return true;

                case VoiceCommandExecutionKind.GatherSupplyWithWorkerFallback:
                    return VoiceGatherSupplyWithWorkerFallback(plan.SupplyKind);

                case VoiceCommandExecutionKind.WorkerOpenBuildThenSlot:
                    return VoiceWorkerOpenBuildThenSlot(plan.UiSlot1Based, plan.UiSecondSlot1Based);

                case VoiceCommandExecutionKind.SelectBuildingThenActionBarSlot:
                    return VoiceSelectBuildingThenUiSlot(plan.BuildingTarget, plan.UiSlot1Based);

                default:
                    return false;
            }
        }

        bool VoiceTryPressUiSlot(int uiSlot1Based, string labelForLog)
        {
            if (CollectSelectedCommandables().Count == 0)
            {
                GameEventLog.Post($"[VoiceCmd] Chọn unit/nhà trước khi dùng lệnh {labelForLog}.", GameEventLogCategory.Warning);
                return false;
            }

            OnHotkeyActionBarSlot(Mathf.Clamp(uiSlot1Based, 1, 9) - 1);
            GameEventLog.Post($"[VoiceCmd] Lệnh {uiSlot1Based} ({labelForLog}).", GameEventLogCategory.Info);
            return true;
        }

        bool VoiceAttackNearbyHostiles()
        {
            List<AbstractUnit> attackers = CollectSelectedAbstractUnits();
            if (attackers.Count == 0)
            {
                GameEventLog.Post("[VoiceCmd] Chọn quân trước khi tấn công.", GameEventLogCategory.Warning);
                return false;
            }

            attackers.RemoveAll(static unit => unit is not IAttacker);
            if (attackers.Count == 0)
            {
                GameEventLog.Post("[VoiceCmd] Unit đang chọn không thể tấn công.", GameEventLogCategory.Warning);
                return false;
            }

            if (!VoiceTryFindNearestHostile(attackers, out AbstractUnit hostile, out RaycastHit hit))
            {
                GameEventLog.Post("[VoiceCmd] Không thấy địch gần đơn vị đang chọn.", GameEventLogCategory.Warning);
                return false;
            }

            bool any = false;
            for (int i = 0; i < attackers.Count; i++)
            {
                if (TryDispatchCommandsToUnits(
                        new List<AbstractUnit> { attackers[i] },
                        hit,
                        commandBeingActivated: null,
                        MouseButton.Right))
                {
                    any = true;
                }
            }

            if (any)
            {
                GameEventLog.Post($"[VoiceCmd] Tấn công {hostile.name}.", GameEventLogCategory.Info);
            }

            return any;
        }

        bool VoiceTryFindNearestHostile(
            List<AbstractUnit> referenceUnits,
            out AbstractUnit hostile,
            out RaycastHit hit)
        {
            hostile = null;
            hit = default;
            if (referenceUnits == null || referenceUnits.Count == 0)
            {
                return false;
            }

            Vector3 center = Vector3.zero;
            for (int i = 0; i < referenceUnits.Count; i++)
            {
                center += referenceUnits[i].transform.position;
            }

            center /= referenceUnits.Count;
            Owner localOwner = ResolveLocalOwner();
            float radiusSqr = VoiceAttackScanRadius * VoiceAttackScanRadius;
            float bestSqr = float.MaxValue;

            AbstractUnit[] units = FindObjectsByType<AbstractUnit>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < units.Length; i++)
            {
                AbstractUnit candidate = units[i];
                if (candidate == null
                    || !candidate.gameObject.activeInHierarchy
                    || candidate.Owner == localOwner
                    || candidate.Owner == Owner.Invalid)
                {
                    continue;
                }

                float sqr = (candidate.transform.position - center).sqrMagnitude;
                if (sqr > radiusSqr || sqr >= bestSqr)
                {
                    continue;
                }

                bestSqr = sqr;
                hostile = candidate;
            }

            if (hostile == null)
            {
                return false;
            }

            return VoiceTryBuildHitForWorldPoint(hostile.transform.position, out hit);
        }

        bool VoiceSelectAllUnitsOnScreen()
        {
            DeselectAllUnits();
            int selected = 0;
            selected += VoiceSelectAllOfArchetypeOnScreen("worker");
            selected += VoiceSelectAllOfArchetypeOnScreen("warrior");
            selected += VoiceSelectAllOfArchetypeOnScreen("archer");
            selected += VoiceSelectAllOfArchetypeOnScreen("rockwarrior");

            GameEventLog.Post(
                selected > 0
                    ? $"[VoiceCmd] Đã chọn {selected} unit (gồm dân)."
                    : "[VoiceCmd] Không có unit trên màn hình.",
                selected > 0 ? GameEventLogCategory.Info : GameEventLogCategory.Warning);
            return selected > 0;
        }

        bool VoiceGatherSupplyWithWorkerFallback(AIEconomySupplyKindClassifier.Kind supplyKind)
        {
            if (!VoiceEnsureWorkerSelectionForVoiceCommand())
            {
                GameEventLog.Post("[VoiceCmd] Không có dân rảnh để thu tài nguyên.", GameEventLogCategory.Warning);
                return false;
            }

            return VoiceTryGatherNearestSupply(supplyKind);
        }

        bool VoiceWorkerOpenBuildThenSlot(int openBuildUiSlot, int buildUiSlot)
        {
            if (!VoiceEnsureWorkerSelectionForVoiceCommand())
            {
                GameEventLog.Post("[VoiceCmd] Không có dân rảnh để xây.", GameEventLogCategory.Warning);
                return false;
            }

            if (!VoiceTryPressUiSlot(openBuildUiSlot, "Build menu"))
            {
                return false;
            }

            VoiceTryPressUiSlot(buildUiSlot, "xây dựng");
            return true;
        }

        bool VoiceSelectBuildingThenUiSlot(VoiceCommandBuildingTarget target, int uiSlot1Based)
        {
            if (!VoiceTrySelectLocalBuilding(target))
            {
                GameEventLog.Post("[VoiceCmd] Không tìm thấy tòa nhà phù hợp.", GameEventLogCategory.Warning);
                return false;
            }

            return VoiceTryPressUiSlot(uiSlot1Based, target.ToString());
        }

        /// <summary>
        /// Mục tiêu: Đảm bảo có ít nhất một worker trong selection cho lệnh voice.
        /// Cách hoạt động: Giữ worker đã chọn; nếu không có thì chọn một dân rảnh trên màn hình.
        /// </summary>
        bool VoiceEnsureWorkerSelectionForVoiceCommand()
        {
            if (SelectionHasLocalWorker())
            {
                return true;
            }

            if (!TryResolveUnitPrefab("worker", out GameObject workerPrefab))
            {
                return false;
            }

            List<AbstractUnit> workers = CollectUnitsOfKindOnScreen(workerPrefab);
            for (int i = 0; i < workers.Count; i++)
            {
                if (workers[i] is not Worker worker || !IsVoiceIdleWorker(worker))
                {
                    continue;
                }

                DeselectAllUnits();
                TrySelectLocalOwned(worker);
                return true;
            }

            return false;
        }

        bool SelectionHasLocalWorker()
        {
            List<AbstractUnit> selected = CollectSelectedAbstractUnits();
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] is Worker && IsOwnedByLocalPlayer(selected[i]))
                {
                    return true;
                }
            }

            return false;
        }

        bool VoiceTrySelectLocalBuilding(VoiceCommandBuildingTarget target)
        {
            GameObject prefab = VoiceResolveBuildingPrefab(target);
            if (prefab == null)
            {
                return false;
            }

            BaseBuilding best = null;
            float bestSqr = float.MaxValue;
            Vector3 origin = cameraTarget != null ? cameraTarget.position : Vector3.zero;

            BaseBuilding[] buildings = FindObjectsByType<BaseBuilding>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < buildings.Length; i++)
            {
                BaseBuilding building = buildings[i];
                if (building == null
                    || !IsOwnedByLocalPlayer(building)
                    || !BuildingKindMatching.MatchesPrefab(building, prefab))
                {
                    continue;
                }

                if (target == VoiceCommandBuildingTarget.CivilCentral && !CivilCentralUtility.IsCivilCentral(building))
                {
                    continue;
                }

                float sqr = (building.transform.position - origin).sqrMagnitude;
                if (sqr >= bestSqr)
                {
                    continue;
                }

                bestSqr = sqr;
                best = building;
            }

            if (best == null)
            {
                return false;
            }

            DeselectAllUnits();
            TrySelectLocalOwned(best);
            return true;
        }

        GameObject VoiceResolveBuildingPrefab(VoiceCommandBuildingTarget target)
        {
            switch (target)
            {
                case VoiceCommandBuildingTarget.CivilCentral:
                    return VoiceResolveBuildingPrefabByKey(ref cachedCivilCentralPrefab, "civil_central");
                case VoiceCommandBuildingTarget.Forge:
                    return VoiceResolveBuildingPrefabByKey(ref cachedForgePrefab, "forge");
                case VoiceCommandBuildingTarget.Barracks:
                    return VoiceResolveBuildingPrefabByKey(ref cachedBarrackPrefab, "barrack");
                default:
                    return null;
            }
        }

        GameObject VoiceResolveBuildingPrefabByKey(ref GameObject cache, string key)
        {
            if (cache != null)
            {
                return cache;
            }

            BaseBuilding[] buildings = FindObjectsByType<BaseBuilding>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < buildings.Length; i++)
            {
                BaseBuilding building = buildings[i];
                if (building?.BuildingSO?.Prefab == null || !IsOwnedByLocalPlayer(building))
                {
                    continue;
                }

                string name = RecognizedSpeechPhraseNormalizer.ToDatasetPhraseForm(building.BuildingSO.name);
                if (name.Contains(key, System.StringComparison.Ordinal))
                {
                    cache = building.BuildingSO.Prefab;
                    return cache;
                }
            }

            return null;
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

        bool VoiceTryGatherNearestSupply(AIEconomySupplyKindClassifier.Kind supplyKind)
        {
            List<AbstractUnit> workers = CollectSelectedAbstractUnits();
            workers.RemoveAll(static unit => unit is not Worker);
            if (workers.Count == 0)
            {
                return false;
            }

            if (!VoiceTryFindNearestSupply(supplyKind, out _, out RaycastHit hit))
            {
                GameEventLog.Post("[VoiceCmd] Không tìm mỏ phù hợp gần camera.", GameEventLogCategory.Warning);
                return false;
            }

            bool any = false;
            for (int i = 0; i < workers.Count; i++)
            {
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
                GameEventLog.Post($"[VoiceCmd] Thu {supplyKind}.", GameEventLogCategory.Info);
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

            return VoiceTryBuildHitForWorldPoint(supply.transform.position, out hit);
        }

        bool VoiceTryBuildHitForWorldPoint(Vector3 point, out RaycastHit hit)
        {
            hit = default;
            RaycastHit[] hits = Physics.RaycastAll(
                point + Vector3.up * 25f,
                Vector3.down,
                50f,
                interactableLayers | floorLayers,
                QueryTriggerInteraction.Collide);

            for (int h = 0; h < hits.Length; h++)
            {
                if (hits[h].collider != null)
                {
                    hit = hits[h];
                    return true;
                }
            }

            Ray cameraRay = camera != null
                ? camera.ScreenPointToRay(camera.WorldToScreenPoint(point))
                : default;
            if (camera != null
                && GameplayWorldRaycastUtility.TryGetCommandRaycastHit(
                    cameraRay,
                    interactableLayers | floorLayers,
                    out hit))
            {
                return true;
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

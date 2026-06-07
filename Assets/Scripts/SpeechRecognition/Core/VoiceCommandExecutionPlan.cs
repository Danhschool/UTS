using System;
using GameDevTV.RTS.AI;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Kế hoạch thực thi sau khi STT khớp CommandId — mô tả hành vi gameplay, không chứa Unity API.
    /// </summary>
    public readonly struct VoiceCommandExecutionPlan
    {
        public VoiceCommandExecutionPlan(
            VoiceCommandExecutionKind kind,
            int uiSlot1Based = 0,
            int uiSecondSlot1Based = 0,
            AIEconomySupplyKindClassifier.Kind supplyKind = AIEconomySupplyKindClassifier.Kind.Unknown,
            VoiceCommandBuildingTarget buildingTarget = VoiceCommandBuildingTarget.None,
            int selectCount = 1,
            string unitArchetype = null)
        {
            Kind = kind;
            UiSlot1Based = uiSlot1Based;
            UiSecondSlot1Based = uiSecondSlot1Based;
            SupplyKind = supplyKind;
            BuildingTarget = buildingTarget;
            SelectCount = selectCount;
            UnitArchetype = unitArchetype;
        }

        public VoiceCommandExecutionKind Kind { get; }
        public int UiSlot1Based { get; }
        public int UiSecondSlot1Based { get; }
        public AIEconomySupplyKindClassifier.Kind SupplyKind { get; }
        public VoiceCommandBuildingTarget BuildingTarget { get; }
        public int SelectCount { get; }
        public string UnitArchetype { get; }
    }

    public enum VoiceCommandExecutionKind
    {
        StopSelection,
        ActionBarSlotOnSelection,
        AttackNearbyHostiles,
        SelectAllUnitsOnScreen,
        SelectUnitsByArchetype,
        GatherSupplyWithWorkerFallback,
        WorkerOpenBuildThenSlot,
        SelectBuildingThenActionBarSlot
    }

    public enum VoiceCommandBuildingTarget
    {
        None,
        CivilCentral,
        Forge,
        Barracks
    }

    /// <summary>
    /// SRP: Ánh xạ CommandId → kế hoạch thực thi theo spec voice UTS (slot UI 1-based).
    /// </summary>
    public static class VoiceCommandExecutionPlanCatalog
    {
        /// <summary>
        /// Mục tiêu: Trả về kế hoạch gameplay cho CommandId đã khớp dataset.
        /// Cách hoạt động: Tra bảng tĩnh; parse select_N_archetype; false nếu không hỗ trợ.
        /// </summary>
        public static bool TryGetPlan(string commandId, out VoiceCommandExecutionPlan plan)
        {
            plan = default;
            if (string.IsNullOrWhiteSpace(commandId))
            {
                return false;
            }

            string id = commandId.Trim();
            switch (id)
            {
                case "stop":
                    plan = new VoiceCommandExecutionPlan(VoiceCommandExecutionKind.StopSelection);
                    return true;
                case "move":
                    plan = new VoiceCommandExecutionPlan(VoiceCommandExecutionKind.ActionBarSlotOnSelection, uiSlot1Based: 1);
                    return true;
                case "attack":
                    plan = new VoiceCommandExecutionPlan(VoiceCommandExecutionKind.AttackNearbyHostiles);
                    return true;
                case "select_all_military":
                    plan = new VoiceCommandExecutionPlan(VoiceCommandExecutionKind.SelectAllUnitsOnScreen);
                    return true;
                case "gather_wood":
                    plan = new VoiceCommandExecutionPlan(
                        VoiceCommandExecutionKind.GatherSupplyWithWorkerFallback,
                        supplyKind: AIEconomySupplyKindClassifier.Kind.Wood);
                    return true;
                case "gather_stone":
                    plan = new VoiceCommandExecutionPlan(
                        VoiceCommandExecutionKind.GatherSupplyWithWorkerFallback,
                        supplyKind: AIEconomySupplyKindClassifier.Kind.Stone);
                    return true;
                case "gather_food":
                    plan = new VoiceCommandExecutionPlan(
                        VoiceCommandExecutionKind.GatherSupplyWithWorkerFallback,
                        supplyKind: AIEconomySupplyKindClassifier.Kind.Food);
                    return true;
                case "build_storehouse":
                    plan = new VoiceCommandExecutionPlan(VoiceCommandExecutionKind.WorkerOpenBuildThenSlot, 7, 2);
                    return true;
                case "build_forge":
                    plan = new VoiceCommandExecutionPlan(VoiceCommandExecutionKind.WorkerOpenBuildThenSlot, 7, 5);
                    return true;
                case "build_barracks":
                    plan = new VoiceCommandExecutionPlan(VoiceCommandExecutionKind.WorkerOpenBuildThenSlot, 7, 4);
                    return true;
                case "build_corral":
                    plan = new VoiceCommandExecutionPlan(VoiceCommandExecutionKind.WorkerOpenBuildThenSlot, 7, 3);
                    return true;
                case "build_defense_tower":
                    plan = new VoiceCommandExecutionPlan(VoiceCommandExecutionKind.WorkerOpenBuildThenSlot, 7, 6);
                    return true;
                case "train_worker":
                    plan = new VoiceCommandExecutionPlan(
                        VoiceCommandExecutionKind.SelectBuildingThenActionBarSlot,
                        uiSlot1Based: 1,
                        buildingTarget: VoiceCommandBuildingTarget.CivilCentral);
                    return true;
                case "train_warrior":
                    plan = new VoiceCommandExecutionPlan(
                        VoiceCommandExecutionKind.SelectBuildingThenActionBarSlot,
                        uiSlot1Based: 1,
                        buildingTarget: VoiceCommandBuildingTarget.Barracks);
                    return true;
                case "train_archer":
                    plan = new VoiceCommandExecutionPlan(
                        VoiceCommandExecutionKind.SelectBuildingThenActionBarSlot,
                        uiSlot1Based: 2,
                        buildingTarget: VoiceCommandBuildingTarget.Barracks);
                    return true;
                case "train_rockwarrior":
                    plan = new VoiceCommandExecutionPlan(
                        VoiceCommandExecutionKind.SelectBuildingThenActionBarSlot,
                        uiSlot1Based: 3,
                        buildingTarget: VoiceCommandBuildingTarget.Barracks);
                    return true;
                case "research_damage":
                    plan = new VoiceCommandExecutionPlan(
                        VoiceCommandExecutionKind.SelectBuildingThenActionBarSlot,
                        uiSlot1Based: 1,
                        buildingTarget: VoiceCommandBuildingTarget.Forge);
                    return true;
                case "research_attack_delay":
                    plan = new VoiceCommandExecutionPlan(
                        VoiceCommandExecutionKind.SelectBuildingThenActionBarSlot,
                        uiSlot1Based: 2,
                        buildingTarget: VoiceCommandBuildingTarget.Forge);
                    return true;
                case "research_gather_amount":
                    plan = new VoiceCommandExecutionPlan(
                        VoiceCommandExecutionKind.SelectBuildingThenActionBarSlot,
                        uiSlot1Based: 3,
                        buildingTarget: VoiceCommandBuildingTarget.Forge);
                    return true;
                case "research_gather_time":
                    plan = new VoiceCommandExecutionPlan(
                        VoiceCommandExecutionKind.SelectBuildingThenActionBarSlot,
                        uiSlot1Based: 4,
                        buildingTarget: VoiceCommandBuildingTarget.Forge);
                    return true;
                case "research_health":
                    plan = new VoiceCommandExecutionPlan(
                        VoiceCommandExecutionKind.SelectBuildingThenActionBarSlot,
                        uiSlot1Based: 5,
                        buildingTarget: VoiceCommandBuildingTarget.Forge);
                    return true;
                case "research_move_speed":
                    plan = new VoiceCommandExecutionPlan(
                        VoiceCommandExecutionKind.SelectBuildingThenActionBarSlot,
                        uiSlot1Based: 6,
                        buildingTarget: VoiceCommandBuildingTarget.Forge);
                    return true;
            }

            return TryParseSelectPlan(id, out plan);
        }

        static bool TryParseSelectPlan(string commandId, out VoiceCommandExecutionPlan plan)
        {
            plan = default;
            if (!commandId.StartsWith("select_", StringComparison.Ordinal))
            {
                return false;
            }

            string remainder = commandId.Substring("select_".Length);
            int underscore = remainder.IndexOf('_');
            if (underscore <= 0 || underscore >= remainder.Length - 1)
            {
                return false;
            }

            if (!int.TryParse(remainder.Substring(0, underscore), out int count) || count <= 0)
            {
                return false;
            }

            plan = new VoiceCommandExecutionPlan(
                VoiceCommandExecutionKind.SelectUnitsByArchetype,
                selectCount: count,
                unitArchetype: remainder.Substring(underscore + 1));
            return true;
        }
    }
}

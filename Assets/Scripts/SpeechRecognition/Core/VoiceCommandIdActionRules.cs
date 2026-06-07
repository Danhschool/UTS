using System;
using System.Collections.Generic;
using GameDevTV.RTS.AI;
using GameDevTV.RTS.Commands;
using GameDevTV.RTS.Environment;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace ProjectRTS.SpeechRecognition.Core
{
    /// <summary>
    /// Quy tắc ánh xạ CommandId (JSON voice) → kiểu hành động / predicate tìm BaseCommand trên selection.
    /// </summary>
    public static class VoiceCommandIdActionRules
    {
        public enum VoiceGameplayActionKind
        {
            StopSelection,
            ArmWorldTargetCommand,
            GatherNearestSupply,
            SelectUnits,
            SelectIdleWorkers,
            SelectAllMilitary,
            ActivateUiCommand
        }

        public readonly struct VoiceGameplayAction
        {
            public VoiceGameplayAction(
                VoiceGameplayActionKind kind,
                int selectCount = 1,
                string unitArchetype = null,
                AIEconomySupplyKindClassifier.Kind supplyKind = AIEconomySupplyKindClassifier.Kind.Unknown,
                Func<BaseCommand, bool> commandPredicate = null)
            {
                Kind = kind;
                SelectCount = selectCount;
                UnitArchetype = unitArchetype;
                SupplyKind = supplyKind;
                CommandPredicate = commandPredicate;
            }

            public VoiceGameplayActionKind Kind { get; }
            public int SelectCount { get; }
            public string UnitArchetype { get; }
            public AIEconomySupplyKindClassifier.Kind SupplyKind { get; }
            public Func<BaseCommand, bool> CommandPredicate { get; }
        }

        static readonly Dictionary<string, VoiceGameplayAction> Actions = BuildActions();

        /// <summary>
        /// Mục tiêu: Chuyển CommandId đã nhận diện thành mô tả hành động gameplay.
        /// Cách hoạt động: Tra bảng tĩnh; parse select_N_archetype; fallback predicate theo token commandId.
        /// </summary>
        public static bool TryResolve(string commandId, out VoiceGameplayAction action)
        {
            action = default;
            if (string.IsNullOrWhiteSpace(commandId))
            {
                return false;
            }

            string id = commandId.Trim();
            if (Actions.TryGetValue(id, out action))
            {
                return true;
            }

            if (TryParseSelectCommand(id, out action))
            {
                return true;
            }

            if (TryParseKeywordCommand(id, out action))
            {
                return true;
            }

            return false;
        }

        static Dictionary<string, VoiceGameplayAction> BuildActions()
        {
            return new Dictionary<string, VoiceGameplayAction>(StringComparer.Ordinal)
            {
                ["stop"] = new(VoiceGameplayActionKind.StopSelection),
                ["move"] = new(VoiceGameplayActionKind.ArmWorldTargetCommand, commandPredicate: IsMoveCommand),
                ["attack"] = new(VoiceGameplayActionKind.ArmWorldTargetCommand, commandPredicate: IsAttackCommand),
                ["attack_move"] = new(VoiceGameplayActionKind.ArmWorldTargetCommand, commandPredicate: IsAttackMoveCommand),
                ["hold_position"] = new(VoiceGameplayActionKind.ArmWorldTargetCommand, commandPredicate: IsHoldPositionCommand),
                ["select_all_military"] = new(VoiceGameplayActionKind.SelectAllMilitary),
                ["select_idle_workers"] = new(VoiceGameplayActionKind.SelectIdleWorkers),
                ["gather_wood"] = new(VoiceGameplayActionKind.GatherNearestSupply, supplyKind: AIEconomySupplyKindClassifier.Kind.Wood),
                ["gather_stone"] = new(VoiceGameplayActionKind.GatherNearestSupply, supplyKind: AIEconomySupplyKindClassifier.Kind.Stone),
                ["gather_food"] = new(VoiceGameplayActionKind.GatherNearestSupply, supplyKind: AIEconomySupplyKindClassifier.Kind.Food),
                ["build_storehouse"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: MatchesBuild("storehouse", "kho")),
                ["build_forge"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: MatchesBuild("forge", "ren")),
                ["build_barracks"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: MatchesBuild("barrack", "linh", "doanh")),
                ["build_corral"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: MatchesBuild("corral", "chuong")),
                ["build_defense_tower"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: MatchesBuild("tower", "thap", "canh")),
                ["train_worker"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: MatchesTrain("worker", "dan", "nong")),
                ["train_warrior"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: MatchesTrain("warrior", "kiem")),
                ["train_archer"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: MatchesTrain("archer", "cung")),
                ["train_rockwarrior"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: MatchesTrain("rock", "da binh")),
                ["research_damage"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: MatchesResearch("damage", "sat thuong")),
                ["research_health"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: MatchesResearch("health", "mau", "sinh luc")),
                ["research_move_speed"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: MatchesResearch("move", "toc do di")),
                ["research_attack_delay"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: MatchesResearch("attack delay", "toc do danh")),
                ["research_gather_amount"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: MatchesResearch("gather amount", "luong thu")),
                ["research_gather_time"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: MatchesResearch("gather time", "toc do thu")),
                ["cancel_research"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: MatchesCancel("research", "nghien")),
                ["cancel_production"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: MatchesCancel("production", "san xuat", "train")),
                ["cancel_building"] = new(VoiceGameplayActionKind.ActivateUiCommand, commandPredicate: cmd => cmd is CancelBuildingCommand)
            };
        }

        static bool TryParseSelectCommand(string commandId, out VoiceGameplayAction action)
        {
            action = default;
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

            string countToken = remainder.Substring(0, underscore);
            string archetype = remainder.Substring(underscore + 1);
            if (!int.TryParse(countToken, out int count) || count <= 0)
            {
                return false;
            }

            action = new VoiceGameplayAction(
                VoiceGameplayActionKind.SelectUnits,
                selectCount: count,
                unitArchetype: archetype);
            return true;
        }

        static bool TryParseKeywordCommand(string commandId, out VoiceGameplayAction action)
        {
            action = default;
            return false;
        }

        static Func<BaseCommand, bool> IsMoveCommand => cmd => cmd is MoveCommand;

        static Func<BaseCommand, bool> IsAttackCommand => cmd => cmd is AttackCommand;

        static Func<BaseCommand, bool> IsAttackMoveCommand => cmd =>
            CommandNameContains(cmd, "attack") && CommandNameContains(cmd, "move");

        static Func<BaseCommand, bool> IsHoldPositionCommand => cmd =>
            CommandNameContains(cmd, "hold") || CommandNameContains(cmd, "stand");

        static Func<BaseCommand, bool> MatchesBuild(params string[] tokens) => cmd =>
            cmd is BuildBuildingCommand build
            && build.Building != null
            && NameMatchesAny(build.Building.name, tokens);

        static Func<BaseCommand, bool> MatchesTrain(params string[] tokens) => cmd =>
            cmd is BuildUnitCommand train
            && train.Unit != null
            && NameMatchesAny(train.Unit.name, tokens);

        static Func<BaseCommand, bool> MatchesResearch(params string[] tokens) => cmd =>
            cmd is ResearchUpgradeCommand research
            && research.Upgrade != null
            && NameMatchesAny(research.Upgrade.name, tokens);

        static Func<BaseCommand, bool> MatchesCancel(params string[] tokens) => cmd =>
        {
            if (cmd is CancelBuildingCommand)
            {
                return tokens.Length == 0 || NameMatchesAny("building", tokens);
            }

            string name = cmd != null ? cmd.Name : string.Empty;
            if (!NameMatchesAny(name, "cancel", "huy"))
            {
                return false;
            }

            return NameMatchesAny(name, tokens);
        };

        static bool CommandNameContains(BaseCommand command, string token)
        {
            if (command == null || string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            return NameMatchesAny(command.Name, token);
        }

        static bool NameMatchesAny(string raw, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(raw) || tokens == null || tokens.Length == 0)
            {
                return false;
            }

            string normalized = RecognizedSpeechPhraseNormalizer.ToDatasetPhraseForm(raw);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = RecognizedSpeechPhraseNormalizer.ToDatasetPhraseForm(tokens[i]);
                if (token.Length > 0 && normalized.Contains(token, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

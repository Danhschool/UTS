using GameDevTV.RTS.Player;
using GameDevTV.RTS.TechTree;
using GameDevTV.RTS.Units;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace GameDevTV.RTS.Commands
{
    [CreateAssetMenu(fileName = "Build Building", menuName = "Units/Commands/Build Building")]
    public class BuildBuildingCommand : BaseCommand, IUnlockableCommand
    {
        [field: SerializeField] public BuildingSO Building { get; private set; }

        public override bool CanHandle(CommandContext context)
        {
            // Chỉ qua UI: CommandSelectedEvent → ghost → click trái (ActivateAction). Không cướp chuột phải như Move/Gather/Attack.
            if (context.Commandable is not IBuildingBuilder buildingBuilder || context.Button == MouseButton.Right)
            {
                return false;
            }

            if (TryGetResumeTarget(context, out BaseBuilding _))
            {
                return true;
            }

            if (buildingBuilder.IsBuilding)
            {
                return false;
            }

            return HasEnoughSupplies(context) && AllRestrictionsPass(context.Hit.point);
        }

        /// <summary>
        /// Mục tiêu: Nhận diện click lên công trình đang dở để resume (không trừ tài nguyên lần hai).
        /// Cách hoạt động: Collider có BaseBuilding cùng BuildingSO và CanResumeConstruction.
        /// </summary>
        private bool TryGetResumeTarget(CommandContext context, out BaseBuilding building)
        {
            building = null;
            if (context.Hit.collider == null)
            {
                return false;
            }

            building = context.Hit.collider.GetComponentInParent<BaseBuilding>();
            if (building == null)
            {
                return false;
            }

            if (Building != building.BuildingSO)
            {
                return false;
            }

            return building.CanResumeConstruction();
        }

        public override void Handle(CommandContext context)
        {
            IBuildingBuilder builder = (IBuildingBuilder)context.Commandable;

            if (TryGetResumeTarget(context, out BaseBuilding resumeTarget))
            {
                builder.ResumeBuilding(resumeTarget);
            }
            else if (HasEnoughSupplies(context) && AllRestrictionsPass(context.Hit.point))
            {
                builder.Build(Building, context.Hit.point);
            }
            else if (!HasEnoughSupplies(context))
            {
                SupplyAffordability.WarnPlayerIfInsufficient(
                    context.Owner,
                    Building.Cost,
                    $"xây {Building.Name}");
            }
        }

        public override bool IsLocked(CommandContext context) =>
            !HasEnoughSupplies(context) || !Building.TechTree.IsUnlocked(context.Owner, Building);

        public UnlockableSO[] GetUnmetDependencies(Owner owner)
        {
            return Building.TechTree.GetUnmetDependencies(owner, Building);
        }

        private bool HasEnoughSupplies(CommandContext context) =>
            SupplyAffordability.HasEnough(context.Owner, Building.Cost);
    }
}

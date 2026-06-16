using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Units;
using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using GameDevTV.RTS.Utilities;
using Mirror;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Gather Supplies", story: "[Unit] gathers [Amount] supplies from [GatherableSupplies] .", category: "Action/Units", id: "3b941d7ae99d1e36b7d806875379c977")]
    public partial class GatherSuppliesAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Unit;
        [SerializeReference] public BlackboardVariable<int> Amount;
        [SerializeReference] public BlackboardVariable<GatherableSupply> GatherableSupplies;
        [SerializeReference] public BlackboardVariable<SupplySO> SupplySO;

        private Animator animator;
        private float enterTime;
        private bool didBeginGather;

        protected override Status OnStart()
        {
            didBeginGather = false;

            // Hết node / chưa gán Supply: không fail cả Sequence (để các node sau như về CP vẫn chạy).
            if (GatherableSupplies.Value == null)
            {
                return Status.Success;
            }

            enterTime = Time.time;

            if (Unit.Value != null && Unit.Value.TryGetComponent(out animator))
            {
                animator.SetBool(AnimationConstants.IS_ENGAGING, true);
            }

            GatherableSupplies.Value.BeginGather();
            didBeginGather = true;
            SupplySO.Value = GatherableSupplies.Value.Supply;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (GatherableSupplies.Value == null)
            {
                return Status.Success;
            }

            float baseTime = GatherableSupplies.Value.Supply.BaseGatherTime;
            float mult = 1f;
            if (Unit.Value != null
                && Unit.Value.TryGetComponent(out AbstractCommandable commandable)
                && commandable.UnitSO is UnitSO unitSo)
            {
                mult = Mathf.Max(0.05f, unitSo.GatherTimeMultiplier);
            }

            if (baseTime * mult + enterTime <= Time.time)
            {
                return Status.Success;
            }

            return Status.Running;
        }

        protected override void OnEnd()
        {
            try
            {
                if (animator != null)
                {
                    animator.SetBool(AnimationConstants.IS_ENGAGING, false);
                }

                if (CurrentStatus == Status.Success)
                {
                    if (!didBeginGather)
                    {
                        return;
                    }

                    if (GatherableSupplies.Value == null)
                    {
                        return;
                    }

                    int bonus = 0;
                    if (Unit.Value != null
                        && Unit.Value.TryGetComponent(out AbstractCommandable commandable)
                        && commandable.UnitSO is UnitSO unitSo)
                    {
                        bonus = unitSo.GatherAmountBonus;
                    }

                    GatherableSupply supplyRef = GatherableSupplies.Value;
                    int perTrip = Mathf.Max(0, supplyRef.Supply.AmountPerGather + bonus);
                    int previewGathered = Mathf.Min(perTrip, supplyRef.Amount);
                    Amount.Value = previewGathered;

                    if (Unit.Value != null
                        && Unit.Value.TryGetComponent(out BehaviorGraphAgent graphAgent))
                    {
                        graphAgent.SetVariableValue("SupplyAmountHeld", previewGathered);
                        graphAgent.SetVariableValue("SupplySO", supplyRef.Supply);
                    }

                    if (RtsNetplaySession.IsPureClient)
                    {
                        Amount.Value = previewGathered;
                    }
                    else
                    {
                        Amount.Value = supplyRef.EndGather(bonus);
                    }

                    // #region agent log
                    string ownerName = "unknown";
                    if (Unit.Value != null
                        && Unit.Value.TryGetComponent(out AbstractCommandable ownerCommandable))
                    {
                        ownerName = ownerCommandable.Owner.ToString();
                    }

                    DebugSessionLog013c46.Write(
                        "G1",
                        "GatherSuppliesAction.OnEnd",
                        "gather complete",
                        $"{{\"preview\":{previewGathered},\"final\":{Amount.Value},\"owner\":\"{ownerName}\",\"pureClient\":{RtsNetplaySession.IsPureClient.ToString().ToLowerInvariant()}}}");
                    // #endregion
                    if (supplyRef == null)
                    {
                        GatherableSupplies.Value = null;
                    }
                }
                else if (didBeginGather && GatherableSupplies.Value != null)
                {
                    GatherableSupplies.Value.AbortGather();
                }
            }
            finally
            {
                didBeginGather = false;
            }
        }
    }
}

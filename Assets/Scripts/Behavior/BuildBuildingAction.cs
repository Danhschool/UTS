using GameDevTV.RTS.AI;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Units;
using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

namespace GameDevTV.RTS.Behavior
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Build Building", story: "[Self] builds [BuildingSO] at [TargetLocation] .", category: "Action/Units", id: "32f744f391217b3bc542115e266d16c0")]
    public partial class BuildBuildingAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Self;
        [SerializeReference] public BlackboardVariable<BuildingSO> BuildingSO;
        [SerializeReference] public BlackboardVariable<Vector3> TargetLocation;
        [SerializeReference] public BlackboardVariable<BaseBuilding> BuildingUnderConstruction;

        private float startBuildTime;
        private BaseBuilding completedBuilding;
        private Renderer buildingRenderer;
        private Vector3 rootStartWorld;
        private Vector3 rootEndWorld;
        private float targetHealth;
        private bool animateRootFromBelow;

        protected override Status OnStart()
        {
            if (!HasValidInputs()) return Status.Failure;

            if (BuildingUnderConstruction.Value == null)
            {
                GameObject building = GameObject.Instantiate(BuildingSO.Value.Prefab, TargetLocation.Value, Quaternion.identity);
                if (!building.TryGetComponent(out completedBuilding)
                    || completedBuilding.MainRenderer == null) return Status.Failure;

                if (BuildingSO.Value != null)
                {
                    AIInfraBuildOrderTracker.ClearOrder(completedBuilding.Owner, BuildingSO.Value.Name);
                }
            }
            else
            {
                completedBuilding = BuildingUnderConstruction.Value;
            }

            buildingRenderer = completedBuilding.MainRenderer;
            rootEndWorld = TargetLocation.Value;

            BuildingProgress progressBeforeStart = completedBuilding.Progress;
            bool skipBuriedIntro = progressBeforeStart.State == BuildingProgress.BuildingState.Paused
                || progressBeforeStart.Completion > 0.001f;

            completedBuilding.StartBuilding(Self.Value.GetComponent<IBuildingBuilder>());
            startBuildTime = completedBuilding.Progress.StartTime;
            targetHealth = 0f;

            BuildingUnderConstruction.Value = completedBuilding;

            Bus<BuildingConstructStartedEvent>.Raise(
                completedBuilding.Owner,
                new BuildingConstructStartedEvent(completedBuilding.Owner));

            if (skipBuriedIntro)
            {
                animateRootFromBelow = false;
                completedBuilding.transform.position = rootEndWorld;
            }
            else
            {
                animateRootFromBelow = true;
                completedBuilding.transform.position = rootEndWorld;
                float buryDepth = Mathf.Max(buildingRenderer.bounds.size.y, 0.5f);
                rootStartWorld = rootEndWorld + Vector3.down * buryDepth;
                completedBuilding.transform.position = rootStartWorld;
            }

            return OnUpdate();
        }

        protected override Status OnUpdate()
        {
            if (completedBuilding == null)
            {
                return Status.Failure;
            }

            // Khi đang bị Pause (worker rời công trường), tạm dừng tiến độ build để cho worker có thể làm việc khác.
            if (completedBuilding.Progress.State != BuildingProgress.BuildingState.Building)
            {
                return Status.Running;
            }

            float normalizedTime = (Time.time - completedBuilding.Progress.StartTime) / BuildingSO.Value.BuildTime;

            targetHealth += Time.deltaTime * (BuildingSO.Value.Health / BuildingSO.Value.BuildTime);
            if (targetHealth >= 1)
            {
                int healAmount = Mathf.FloorToInt(targetHealth);
                completedBuilding.Heal(healAmount);
                targetHealth -= healAmount;
            }

            if (animateRootFromBelow)
            {
                completedBuilding.transform.position = Vector3.Lerp(rootStartWorld, rootEndWorld, normalizedTime);
            }

            return normalizedTime >= 1 ? Status.Success : Status.Running;
        }

        protected override void OnEnd()
        {
            if (CurrentStatus == Status.Success)
            {
                if (completedBuilding != null && animateRootFromBelow)
                {
                    completedBuilding.transform.position = rootEndWorld;
                }

                if (completedBuilding != null)
                {
                    completedBuilding.CompleteConstruction();
                    completedBuilding.enabled = true;
                }
            }
        }

        private bool HasValidInputs()
        {
            return Self.Value != null
                && BuildingSO.Value != null
                && BuildingSO.Value.Prefab != null;
        }
    }
}

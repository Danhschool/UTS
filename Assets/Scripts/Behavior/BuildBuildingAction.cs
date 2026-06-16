using GameDevTV.RTS.AI;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Game;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.Units;
using Mirror;
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
        private bool clientPresentationMode;

        protected override Status OnStart()
        {
            if (!HasValidInputs())
            {
                return Status.Failure;
            }

            clientPresentationMode = RtsNetplaySession.IsNetworkMatch
                && !RtsNetplaySession.ShouldRunAuthoritativeGameplay;

            RtsUtsNetworkEntity workerEntity = null;
            NetworkConnectionToClient connection = null;

            if (clientPresentationMode)
            {
                return OnUpdateClientPresentation();
            }

            if (BuildingUnderConstruction.Value == null)
            {
                Worker worker = Self.Value != null ? Self.Value.GetComponent<Worker>() : null;
                Owner owner = worker != null ? worker.Owner : Owner.Invalid;
                if (worker != null && worker.TryGetComponent(out workerEntity))
                {
                    connection = workerEntity.ResolveOwnerConnection();
                }

                if (RtsNetplaySession.IsNetworkMatch)
                {
                    if (!RtsUtsServerEntityFactory.TrySpawnBuilding(
                            BuildingSO.Value,
                            TargetLocation.Value,
                            Quaternion.identity,
                            owner,
                            connection,
                            out completedBuilding)
                        || completedBuilding.MainRenderer == null)
                    {
                        return Status.Failure;
                    }
                }
                else
                {
                    GameObject building = GameObject.Instantiate(BuildingSO.Value.Prefab, TargetLocation.Value, Quaternion.identity);
                    if (!building.TryGetComponent(out completedBuilding)
                        || completedBuilding.MainRenderer == null)
                    {
                        return Status.Failure;
                    }
                }

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

            if (RtsNetplaySession.IsNetworkMatch && NetworkServer.active)
            {
                GameMatchOverlayStateSync.BroadcastBuildingConstructStarted(completedBuilding.Owner);
            }

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

            if (RtsNetplaySession.IsNetworkMatch
                && NetworkServer.active
                && workerEntity != null
                && completedBuilding.TryGetComponent(out NetworkIdentity buildingIdentity))
            {
                workerEntity.RpcLinkClientPresentationBuilding(
                    buildingIdentity.netId,
                    rootEndWorld,
                    completedBuilding.Progress.Completion,
                    BuildingSO.Value.BuildTime);
            }

            return OnUpdate();
        }

        protected override Status OnUpdate()
        {
            if (clientPresentationMode)
            {
                return OnUpdateClientPresentation();
            }

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

            completedBuilding.SetConstructionCompletion(normalizedTime);

            return normalizedTime >= 1 ? Status.Success : Status.Running;
        }

        protected override void OnEnd()
        {
            if (CurrentStatus == Status.Success)
            {
                if (clientPresentationMode)
                {
                    if (Self.Value != null && Self.Value.TryGetComponent(out Worker worker))
                    {
                        worker.ReleasePlannerControlAfterConstructionEnded();
                    }

                    return;
                }

                if (completedBuilding != null && animateRootFromBelow)
                {
                    completedBuilding.transform.position = rootEndWorld;
                }

                if (completedBuilding != null)
                {
                    completedBuilding.CompleteConstruction();
                    completedBuilding.enabled = true;

                    if (RtsNetplaySession.IsNetworkMatch && NetworkServer.active)
                    {
                        if (completedBuilding.TryGetComponent(out RtsUtsNetworkEntity buildingNetEntity))
                        {
                            buildingNetEntity.RpcNotifyBuildingConstructionCompleted();
                        }

                        if (Self.Value != null
                            && Self.Value.TryGetComponent(out RtsUtsNetworkEntity workerNetEntity))
                        {
                            workerNetEntity.RpcNotifyWorkerBuildPresentationComplete();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Pure client — worker BT chờ nhà replicate từ server, không spawn local.
        /// Cách hoạt động: Tìm BaseBuilding khớp SO gần TargetLocation; Running đến khi Completed.
        /// </summary>
        Status OnUpdateClientPresentation()
        {
            if (BuildingUnderConstruction.Value == null)
            {
                if (!TryResolveClientPresentationBuilding(out completedBuilding))
                {
                    return Status.Running;
                }

                BuildingUnderConstruction.Value = completedBuilding;
            }
            else
            {
                completedBuilding = BuildingUnderConstruction.Value;
            }

            if (completedBuilding == null)
            {
                return Status.Running;
            }

            if (completedBuilding.Progress.State == BuildingProgress.BuildingState.Completed)
            {
                return Status.Success;
            }

            if (completedBuilding.Progress.State == BuildingProgress.BuildingState.Building
                || completedBuilding.Progress.State == BuildingProgress.BuildingState.Paused)
            {
                return Status.Running;
            }

            return Status.Running;
        }

        bool TryResolveClientPresentationBuilding(out BaseBuilding building)
        {
            building = null;
            if (BuildingSO.Value == null)
            {
                return false;
            }

            Vector3 target = TargetLocation.Value;
            const float horizontalRadius = 6f;
            float horizontalRadiusSqr = horizontalRadius * horizontalRadius;
            string targetBuildingName = BuildingSO.Value.Name;

            BaseBuilding[] buildings = UnityEngine.Object.FindObjectsByType<BaseBuilding>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            float bestDistSqr = float.MaxValue;
            BaseBuilding best = null;

            for (int i = 0; i < buildings.Length; i++)
            {
                BaseBuilding candidate = buildings[i];
                if (candidate == null || candidate.BuildingSO == null)
                {
                    continue;
                }

                if (candidate.BuildingSO.Name != targetBuildingName)
                {
                    continue;
                }

                Vector3 delta = candidate.transform.position - target;
                delta.y = 0f;
                float distSqr = delta.sqrMagnitude;
                if (distSqr > horizontalRadiusSqr)
                {
                    continue;
                }

                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    best = candidate;
                }
            }

            if (best == null)
            {
                return false;
            }

            building = best;
            return true;
        }

        private bool HasValidInputs()
        {
            return Self.Value != null
                && BuildingSO.Value != null
                && BuildingSO.Value.Prefab != null;
        }
    }
}

using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Game;
using GameDevTV.RTS.Game.FactionSummary;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.UI.InGame
{
    /// <summary>
    /// SRP: Phát hiện kết thúc trận (CC bị phá / đầu hàng) và mở panel tóm tắt 2 phe.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchOutcomeDetector : MonoBehaviour
    {
        static MatchOutcomeDetector instance;
        static bool matchEnded;

        public static MatchOutcomeDetector Instance => instance;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        void OnEnable()
        {
            Bus<BuildingDeathEvent>.RegisterForAll(HandleBuildingDeath);
        }

        void OnDisable()
        {
            Bus<BuildingDeathEvent>.UnregisterForAll(HandleBuildingDeath);
        }

        void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            if (matchEnded || GameMatchOverlayStateSync.HasMatchEndBeenBroadcast)
            {
                return;
            }

            if (evt.Building == null || !CivilCentralUtility.IsCivilCentral(evt.Building))
            {
                return;
            }

            if (GameMatchOverlayStateSync.IsNetworkMatchActive)
            {
                if (NetworkServer.active)
                {
                    GameMatchOverlayStateSync.RequestMatchEndFromCivilCentralDestroyed(evt.Owner);
                }

                return;
            }

            Owner localOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            MatchOutcomeResult result = MatchOutcomeResolver.ResolveFromCivilCentralDestroyed(evt.Owner, localOwner);
            Owner opponentOwner = ResolveOpponentForOutcome(evt.Owner, localOwner);
            ShowOutcome(result, localOwner, opponentOwner);
        }

        /// <summary>
        /// Mục tiêu: Client MP nhận kết quả khi server phát hiện CC bị phá.
        /// Cách hoạt động: Suy thắng/thua theo localOwner rồi load scene End.
        /// </summary>
        public static void ApplyNetworkCivilCentralDestroyed(Owner destroyedOwner)
        {
            if (matchEnded)
            {
                return;
            }

            matchEnded = true;

            Owner localOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            MatchOutcomeResult result = MatchOutcomeResolver.ResolveFromCivilCentralDestroyed(destroyedOwner, localOwner);
            Owner opponentOwner = ResolveOpponentForOutcomeStatic(destroyedOwner, localOwner);
            ShowOutcomeStatic(result, localOwner, opponentOwner, null);
        }

        /// <summary>
        /// Mục tiêu: Đầu hàng — load scene End với kết quả thua và thống kê 2 phe.
        /// Cách hoạt động: Lưu session state rồi gọi MatchOutcomeFlow.LoadEndScene.
        /// </summary>
        public static void ShowSurrenderDefeat(string endSceneName = null)
        {
            if (matchEnded)
            {
                return;
            }

            Owner localOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            FactionSummaryTracker tracker = FactionSummaryTracker.EnsureExists();
            Owner opponentOwner = tracker.FindOpponentOwner(localOwner);
            ShowOutcomeStatic(MatchOutcomeResult.Defeat, localOwner, opponentOwner, endSceneName);
        }

        static void ShowOutcome(MatchOutcomeResult result, Owner localOwner, Owner opponentOwner)
        {
            ShowOutcomeStatic(result, localOwner, opponentOwner, null);
        }

        public static MatchOutcomeDetector EnsureExists()
        {
            if (instance != null)
            {
                return instance;
            }

            MatchOutcomeDetector existing = FindFirstObjectByType<MatchOutcomeDetector>(FindObjectsInactive.Include);
            if (existing != null)
            {
                instance = existing;
                return existing;
            }

            var host = new GameObject(nameof(MatchOutcomeDetector));
            return host.AddComponent<MatchOutcomeDetector>();
        }

        /// <summary>Mục tiêu: Vào trận MP/PvE mới — cho phép kết thúc trận lại.</summary>
        public static void ResetForNewMatch() => matchEnded = false;

        static void ShowOutcomeStatic(
            MatchOutcomeResult result,
            Owner localOwner,
            Owner opponentOwner,
            string endSceneName)
        {
            matchEnded = true;

            FactionSummaryTracker tracker = FactionSummaryTracker.EnsureExists();
            var localSnapshotScratch = new FactionSummarySnapshot();
            var opponentSnapshotScratch = new FactionSummarySnapshot();
            if (tracker != null)
            {
                tracker.BuildSnapshot(localOwner, localSnapshotScratch);
                tracker.BuildSnapshot(opponentOwner, opponentSnapshotScratch);
            }

            FactionSummarySnapshot localSummary = FactionSummarySnapshot.CloneFrom(localSnapshotScratch);
            FactionSummarySnapshot opponentSummary = FactionSummarySnapshot.CloneFrom(opponentSnapshotScratch);
            string localLabel = MatchOutcomeResolver.GetFactionLabel(localOwner, localOwner);
            string opponentLabel = MatchOutcomeResolver.GetFactionLabel(opponentOwner, localOwner);

            MatchOutcomeSessionState.Set(
                result,
                localOwner,
                opponentOwner,
                localLabel,
                opponentLabel,
                localSummary,
                opponentSummary);

            FactionSummaryTracker.DestroyRuntimeInstance();

            string targetEndScene = string.IsNullOrWhiteSpace(endSceneName)
                ? MatchOutcomeFlow.ActiveEndSceneName
                : endSceneName.Trim();

            if (!Application.CanStreamedLevelBeLoaded(targetEndScene))
            {
                Debug.LogWarning(
                    $"[MatchOutcomeDetector] Scene '{targetEndScene}' chưa có trong Build Settings — fallback MainMenu.");
                MatchOutcomeFlow.LoadMenuScene();
                return;
            }

            MatchOutcomeFlow.LoadEndScene(targetEndScene);
        }

        static Owner ResolveOpponentForOutcome(Owner destroyedOwner, Owner localOwner) =>
            ResolveOpponentForOutcomeStatic(destroyedOwner, localOwner);

        static Owner ResolveOpponentForOutcomeStatic(Owner destroyedOwner, Owner localOwner)
        {
            if (destroyedOwner != localOwner)
            {
                return destroyedOwner;
            }

            FactionSummaryTracker tracker = FactionSummaryTracker.EnsureExists();
            if (tracker != null)
            {
                return tracker.FindOpponentOwner(localOwner);
            }

            return localOwner == Owner.Player1 ? Owner.Player2 : Owner.Player1;
        }
    }
}

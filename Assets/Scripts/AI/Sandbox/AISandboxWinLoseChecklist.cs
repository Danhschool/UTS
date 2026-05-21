using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.UI.GameEventLog;
using GameDevTV.RTS.Units;
using GameDevTV.RTS.Utilities;
using UnityEngine;

namespace GameDevTV.RTS.AI
{
    /// <summary>
    /// SRP: Theo dõi win/lose Civil Central trong scene sandbox — tick checklist cho QA.
    /// Gắn cùng GameObject có <see cref="PlayerGameEventLogListener"/> hoặc GameEventLog.
    /// </summary>
    public sealed class AISandboxWinLoseChecklist : MonoBehaviour
    {
        [SerializeField] private Owner playerPerspective = Owner.Player1;
        [SerializeField] private bool logChecklistToGameEvent = true;

        [Header("Checklist — Civil Central win/lose")]
        [SerializeField] private bool sawWinMessage;
        [SerializeField] private bool sawLoseMessage;
        [SerializeField] private bool enemyCivilCentralDestroyed;
        [SerializeField] private bool playerCivilCentralDestroyed;

        public bool SawWinMessage => sawWinMessage;
        public bool SawLoseMessage => sawLoseMessage;
        public bool EnemyCivilCentralDestroyed => enemyCivilCentralDestroyed;
        public bool PlayerCivilCentralDestroyed => playerCivilCentralDestroyed;
        /// <summary>True sau khi test thắng: phá CC địch, nhà Player1 còn.</summary>
        public bool WinScenarioVerified => enemyCivilCentralDestroyed && !playerCivilCentralDestroyed;

        private void OnEnable()
        {
            Bus<BuildingDeathEvent>.RegisterForAll(HandleBuildingDeath);
        }

        private void OnDisable()
        {
            Bus<BuildingDeathEvent>.UnregisterForAll(HandleBuildingDeath);
        }

        /// <summary>
        /// Mục tiêu: Ghi nhận phá/hủy Civil Central và thông báo thắng/thua (góc nhìn Player1).
        /// Cách hoạt động: IsCivilCentral + so owner với playerPerspective; Post checklist nếu bật log.
        /// </summary>
        private void HandleBuildingDeath(BuildingDeathEvent evt)
        {
            if (evt.Building == null || !CivilCentralUtility.IsCivilCentral(evt.Building))
            {
                return;
            }

            if (evt.Owner == playerPerspective)
            {
                playerCivilCentralDestroyed = true;
                sawLoseMessage = true;
                PostChecklist("FAIL: Player Civil Central destroyed — expect THUA in event log.");
            }
            else
            {
                enemyCivilCentralDestroyed = true;
                sawWinMessage = true;
                PostChecklist("PASS: Enemy Civil Central destroyed — expect CHIẾN THẮNG in event log.");
            }
        }

        private void PostChecklist(string message)
        {
            if (!logChecklistToGameEvent)
            {
                return;
            }

            GameEventLog.Post($"[Sandbox CC] {message}", GameEventLogCategory.Info);
        }

        /// <summary>
        /// Mục tiêu: Reset checklist khi chạy lại Play Mode.
        /// Cách hoạt động: Xóa cờ serialized runtime (Inspector / test harness).
        /// </summary>
        [ContextMenu("Reset Civil Central Checklist")]
        public void ResetChecklist()
        {
            sawWinMessage = false;
            sawLoseMessage = false;
            enemyCivilCentralDestroyed = false;
            playerCivilCentralDestroyed = false;
        }
    }
}

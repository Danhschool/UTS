using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Điểm gọi refresh presentation — MP Director ưu tiên, offline dùng PlayerViewBinder.
    /// </summary>
    public static class LocalHumanPresentationRefresh
    {
        /// <summary>
        /// Mục tiêu: Sau load scene MP — bật đúng fog/UI/input theo LocalOwner.
        /// Cách hoạt động: Director nếu có rig; không thì PlayerViewBinder (Game 1).
        /// </summary>
        public static void RefreshFromLocalOwner()
        {
            if (!GameDevTV.RTS.Netplay.MpLocalOwnerSceneSync.EnsureLocalOwnerInitialized(out Owner localOwner))
            {
                return;
            }

            MpPlayerPresentationDirector director =
                GameDevTV.RTS.Netplay.MpFogRefreshThrottle.ResolvePresentationDirector();

            if (director != null && director.HasConfiguredRigs)
            {
                director.RefreshFromLocalOwner();
                return;
            }

            PlayerViewBinder binder =
                Object.FindFirstObjectByType<PlayerViewBinder>(FindObjectsInactive.Include);
            binder?.Apply(localOwner);

            FactionVisibilityUpdater visibilityUpdater =
                Object.FindFirstObjectByType<FactionVisibilityUpdater>(FindObjectsInactive.Include);

            visibilityUpdater?.BindLocalOwner(localOwner);
            visibilityUpdater?.RebuildHideablesForLocalOwner();
        }
    }
}

using GameDevTV.RTS.Player;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Giới hạn tần suất refresh fog/visibility MP — tránh lag do FindObjects + full pass lặp.
    /// </summary>
    public static class MpFogRefreshThrottle
    {
        const int MinFramesBetweenVisibilityRefresh = 45;
        const int MinFramesBetweenPresentationApply = 30;

        static int _nextVisibilityRefreshFrame;
        static int _nextPresentationApplyFrame;
        static FactionVisibilityUpdater _cachedLocalVisibilityUpdater;
        static MpPlayerPresentationDirector _cachedPresentationDirector;

        public static bool ShouldRunVisibilityRefresh(bool force = false)
        {
            if (force)
            {
                return true;
            }

            int frame = Time.frameCount;
            if (frame < _nextVisibilityRefreshFrame)
            {
                return false;
            }

            _nextVisibilityRefreshFrame = frame + MinFramesBetweenVisibilityRefresh;
            return true;
        }

        public static bool ShouldRunPresentationApply(bool ownerChanged)
        {
            if (ownerChanged)
            {
                return true;
            }

            int frame = Time.frameCount;
            if (frame < _nextPresentationApplyFrame)
            {
                return false;
            }

            _nextPresentationApplyFrame = frame + MinFramesBetweenPresentationApply;
            return true;
        }

        public static void InvalidateCaches()
        {
            _cachedLocalVisibilityUpdater = null;
            _cachedPresentationDirector = null;
        }

        public static MpPlayerPresentationDirector ResolvePresentationDirector()
        {
            if (_cachedPresentationDirector != null)
            {
                return _cachedPresentationDirector;
            }

            _cachedPresentationDirector = Object.FindFirstObjectByType<MpPlayerPresentationDirector>(
                FindObjectsInactive.Include);
            return _cachedPresentationDirector;
        }

        public static FactionVisibilityUpdater ResolveLocalVisibilityUpdater()
        {
            if (_cachedLocalVisibilityUpdater != null
                && _cachedLocalVisibilityUpdater.isActiveAndEnabled)
            {
                return _cachedLocalVisibilityUpdater;
            }

            LocalHumanOwnerService service = LocalHumanOwnerService.Instance;
            if (service == null || !service.IsInitialized)
            {
                return null;
            }

            FactionFogPresentation[] presentations = Object.FindObjectsByType<FactionFogPresentation>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < presentations.Length; i++)
            {
                FactionFogPresentation presentation = presentations[i];
                if (presentation == null
                    || presentation.PresentationOwner != service.LocalOwner
                    || !presentation.isActiveAndEnabled
                    || !presentation.gameObject.activeInHierarchy)
                {
                    continue;
                }

                _cachedLocalVisibilityUpdater =
                    presentation.GetComponentInChildren<FactionVisibilityUpdater>(true);
                return _cachedLocalVisibilityUpdater;
            }

            return null;
        }
    }
}

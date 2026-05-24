using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Đảm bảo mọi <see cref="FactionFogSystemReference"/> trong scene đăng ký registry khi load.
    /// </summary>
    public sealed class FactionFogSystemsBootstrap : MonoBehaviour
    {
        [SerializeField] FactionFogSystemReference[] fogSystems = System.Array.Empty<FactionFogSystemReference>();

        void Awake()
        {
            if (fogSystems == null || fogSystems.Length == 0)
            {
                fogSystems = FindObjectsByType<FactionFogSystemReference>(FindObjectsSortMode.None);
            }

            if (fogSystems == null || fogSystems.Length == 0)
            {
                TryAttachLegacyFogReference();
                fogSystems = FindObjectsByType<FactionFogSystemReference>(FindObjectsSortMode.None);
            }

            for (int i = 0; i < fogSystems.Length; i++)
            {
                if (fogSystems[i] != null)
                {
                    fogSystems[i].EnsureReferences();
                }
            }
        }

        /// <summary>
        /// Mục tiêu: Game 1 cũ chỉ có FogVisibilityManager — tự gắn registry P1 không sửa prefab YAML.
        /// Cách hoạt động: Tìm FactionVisibilityUpdater, AddComponent reference trên root fog.
        /// </summary>
        static void TryAttachLegacyFogReference()
        {
            FactionVisibilityUpdater updater = FindFirstObjectByType<FactionVisibilityUpdater>(FindObjectsInactive.Include);
            if (updater == null)
            {
                return;
            }

            Transform fogRoot = updater.transform;
            while (fogRoot.parent != null)
            {
                fogRoot = fogRoot.parent;
            }

            if (fogRoot.GetComponentInChildren<FactionFogSystemReference>(true) != null)
            {
                return;
            }

            var reference = fogRoot.gameObject.AddComponent<FactionFogSystemReference>();
            reference.ConfigureFaction(Owner.Player1);
        }
    }
}

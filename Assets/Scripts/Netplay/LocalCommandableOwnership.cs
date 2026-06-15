using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Resolve phe thật của unit/building trên client MP — ưu tiên SyncVar UtsOwner.
    /// </summary>
    public static class LocalCommandableOwnership
    {
        /// <summary>
        /// Mục tiêu: Client P2 chọn/ra lệnh đúng unit sau spawn replicate.
        /// Cách hoạt động: Mirror active → UtsOwner nếu hợp lệ; không thì Owner trên commandable.
        /// </summary>
        public static Owner ResolveOwner(AbstractCommandable commandable)
        {
            if (commandable == null)
            {
                return Owner.Invalid;
            }

            if (NetworkClient.active
                && commandable.TryGetComponent(out RtsUtsNetworkEntity networkEntity)
                && networkEntity.UtsOwner != Owner.Invalid)
            {
                return networkEntity.UtsOwner;
            }

            return commandable.Owner;
        }

        /// <summary>
        /// Mục tiêu: Input/HUD chỉ tương tác entity thuộc human local trên máy này.
        /// Cách hoạt động: So khớp ResolveOwner với LocalHumanOwnerAccess.
        /// </summary>
        public static bool IsOwnedByLocalHuman(AbstractCommandable commandable)
        {
            if (commandable == null)
            {
                return false;
            }

            Owner local = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            if (local == Owner.Invalid)
            {
                return false;
            }

            return ResolveOwner(commandable) == local;
        }
    }
}

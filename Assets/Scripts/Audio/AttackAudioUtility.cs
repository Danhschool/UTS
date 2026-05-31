using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Audio
{
    /// <summary>
    /// SRP: Chọn cue tấn công theo <see cref="AttackConfigSO"/>.
    /// </summary>
    public static class AttackAudioUtility
    {
        /// <summary>
        /// Mục tiêu: Archer bắn cung vs lính cận chiến có SFX khác nhau.
        /// Cách hoạt động: Ưu tiên AttackAudioCue trên config; fallback projectile → bow, còn lại → melee.
        /// </summary>
        public static AudioCueId ResolveAttackCue(AttackConfigSO config)
        {
            if (config == null)
            {
                return AudioCueId.None;
            }

            if (config.AttackAudioCue != AudioCueId.None)
            {
                return config.AttackAudioCue;
            }

            return config.HasProjectileAttacks ? AudioCueId.AttackBow : AudioCueId.AttackMelee;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.Units
{
    [CreateAssetMenu(fileName = "Unit Death Config", menuName = "Units/Unit Death Config", order = 8)]
    public class UnitDeathConfigSO : ScriptableObject
    {
        [Header("Animator")]
        [field: SerializeField] public string AnimatorBoolParameter { get; private set; } = "isDying";

        [Tooltip("Tên state trong Animator Controller (vd. Dying 0 cho Worker).")]
        [field: SerializeField] public string PrimaryDeathStateName { get; private set; } = "Dying";

        [Tooltip("Tên state/clip thay thế (mỗi unit có thể khác).")]
        [field: SerializeField] public string[] AlternateDeathStateNames { get; private set; } = System.Array.Empty<string>();

        [Header("Thời gian")]
        [field: SerializeField, Tooltip("Bật: chờ Animation Event OnDieAnimationEvent. Tắt: tự detect hết clip.")]
        public bool CompleteViaAnimationEvent { get; private set; } = true;

        [field: SerializeField, Range(0.85f, 1f)]
        public float ClipEndNormalizedTime { get; private set; } = 0.98f;

        [field: SerializeField, Tooltip("Giữ pose chết trên frame cuối trước khi chìm (tránh nhảy về Idle).")]
        public float HoldDeathPoseSeconds { get; private set; } = 0.35f;

        [field: SerializeField] public float AnimationFallbackSeconds { get; private set; } = 2f;

        [field: SerializeField, Tooltip("Giới hạn an toàn nếu không detect được hết clip (thường ≈ độ dài clip + 0.15s).")]
        public float AnimationMaxWaitSeconds { get; private set; } = 4f;

        [Header("Chìm xuống đất")]
        [field: SerializeField] public float SinkDepth { get; private set; } = 1.5f;
        [field: SerializeField] public float SinkDuration { get; private set; } = 1f;

        public int AnimatorBoolHash => Animator.StringToHash(AnimatorBoolParameter);

        public int PrimaryDeathStateHash => Animator.StringToHash(PrimaryDeathStateName);

        /// <summary>
        /// Mục tiêu: liệt kê tên state chết để thử trên Animator (Primary + Alternate).
        /// Cách hoạt động: trả về danh sách tên không trùng, bỏ qua chuỗi rỗng.
        /// </summary>
        public string[] GetDeathStateNamesToTry()
        {
            List<string> names = new() { PrimaryDeathStateName };

            if (AlternateDeathStateNames != null)
            {
                for (int i = 0; i < AlternateDeathStateNames.Length; i++)
                {
                    string alternate = AlternateDeathStateNames[i];
                    if (!string.IsNullOrEmpty(alternate) && !names.Contains(alternate))
                    {
                        names.Add(alternate);
                    }
                }
            }

            return names.ToArray();
        }

        /// <summary>
        /// Mục tiêu: nhận diện state chết trên Animator cho mọi unit (tên state có thể khác nhau).
        /// Cách hoạt động: so khớp shortNameHash / IsName với Primary + danh sách Alternate.
        /// </summary>
        public bool MatchesAnimatorState(AnimatorStateInfo state)
        {
            if (state.IsName(PrimaryDeathStateName) || state.shortNameHash == PrimaryDeathStateHash)
            {
                return true;
            }

            if (AlternateDeathStateNames == null)
            {
                return false;
            }

            for (int i = 0; i < AlternateDeathStateNames.Length; i++)
            {
                string name = AlternateDeathStateNames[i];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (state.IsName(name) || state.shortNameHash == Animator.StringToHash(name))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Mục tiêu: tìm clip chết trong Animator để tính thời gian fallback.
        /// Cách hoạt động: so tên clip với Primary / Alternate (contains hoặc bằng).
        /// </summary>
        public bool MatchesClipName(string clipName)
        {
            if (string.IsNullOrEmpty(clipName))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(PrimaryDeathStateName)
                && (clipName == PrimaryDeathStateName
                    || clipName.StartsWith(PrimaryDeathStateName, System.StringComparison.Ordinal)))
            {
                return true;
            }

            if (AlternateDeathStateNames != null)
            {
                for (int i = 0; i < AlternateDeathStateNames.Length; i++)
                {
                    string name = AlternateDeathStateNames[i];
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    if (clipName == name || clipName.StartsWith(name, System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            // State "Dying 0" thường dùng clip tên "Dying" từ FBX.
            return clipName == "Dying" || clipName.StartsWith("Dying", System.StringComparison.Ordinal);
        }
    }
}

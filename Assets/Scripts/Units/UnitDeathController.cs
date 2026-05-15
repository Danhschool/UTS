using System.Collections;
using GameDevTV.RTS.Utilities;
using UnityEngine;
using UnityEngine.AI;

namespace GameDevTV.RTS.Units
{
    /// <summary>
    /// API chết từng bước cho Behavior Graph (Cách A): prepare → animator → wait → freeze → hold → sink → destroy (node riêng).
    /// </summary>
    [DisallowMultipleComponent]
    public class UnitDeathController : MonoBehaviour
    {
        [SerializeField] private Transform deathSinkTransform;

        private UnitDeathConfigSO deathConfig;
        private Animator animator;
        private NavMeshAgent agent;
        private bool isPrepared;
        private bool isWaitingAnimation;
        private bool isHoldingPose;
        private bool isSinking;
        private bool animationEventReceived;
        private float phaseTimer;
        private float playingAnimationFallbackTimer;
        private int resolvedDeathStateHash;
        private Vector3 sinkStart;
        private Vector3 sinkEnd;

        public bool IsDeathSequenceActive => isPrepared;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            agent = GetComponent<NavMeshAgent>();

            if (deathSinkTransform == null)
            {
                deathSinkTransform = transform;
            }
        }

        public void Configure(UnitDeathConfigSO config, UnitDeathConfigSO overrideConfig)
        {
            deathConfig = overrideConfig != null ? overrideConfig : config;
        }

        /// <summary>
        /// Mục tiêu: tắt gameplay, giữ model để chơi animation chết.
        /// Cách hoạt động: vô hiệu agent, sensor, collider và ẩn child không thuộc model chìm.
        /// </summary>
        public void PrepareDeath()
        {
            if (isPrepared)
            {
                return;
            }

            DisableGameplay();
            isPrepared = true;
        }

        /// <summary>
        /// Mục tiêu: kích animation chết qua bool Animator (Any State → Dying).
        /// Cách hoạt động: tắt bool di chuyển/tấn công, bật isDying; không CrossFade để tránh chạy clip hai lần.
        /// </summary>
        public void SetDeathAnimator()
        {
            if (animator == null)
            {
                return;
            }

            int boolHash = deathConfig != null
                ? deathConfig.AnimatorBoolHash
                : AnimationConstants.IS_DYING;

            animator.speed = 1f;
            animator.SetBool(AnimationConstants.IS_MOVING, false);
            animator.SetBool(AnimationConstants.IS_ATTACK, false);
            animator.SetBool(AnimationConstants.IS_ENGAGING, false);
            animator.SetBool(boolHash, true);
        }

        /// <summary>
        /// Mục tiêu: bắt đầu chờ clip chết kết thúc.
        /// Cách hoạt động: reset event/fallback timer và bật cờ chờ cho TickWaitDeathAnimation.
        /// </summary>
        public void BeginWaitDeathAnimation()
        {
            animationEventReceived = false;
            phaseTimer = 0f;
            resolvedDeathStateHash = TryResolveDeathStateHash(out int hash) ? hash : 0;

            float clipLength = ResolveDeathClipLength();
            playingAnimationFallbackTimer = Mathf.Min(
                clipLength + 0.15f,
                GetAnimationMaxWaitSeconds());

            isWaitingAnimation = true;
        }

        /// <summary>
        /// Mục tiêu: cập nhật chờ animation; trả true khi vẫn đang chờ.
        /// Cách hoạt động: kiểm tra normalizedTime / Animation Event / fallback timer.
        /// </summary>
        public bool TickWaitDeathAnimation(float deltaTime)
        {
            if (!isWaitingAnimation)
            {
                return false;
            }

            playingAnimationFallbackTimer -= deltaTime;

            bool clipEnded = false;
            if (animator != null)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                bool inDeathState = IsDeathAnimatorState(state)
                    || (resolvedDeathStateHash != 0 && state.shortNameHash == resolvedDeathStateHash);

                clipEnded = inDeathState
                    && state.normalizedTime >= GetClipEndThreshold()
                    && !animator.IsInTransition(0);
            }

            bool useEvent = deathConfig == null || deathConfig.CompleteViaAnimationEvent;
            bool shouldFinish = clipEnded || playingAnimationFallbackTimer <= 0f;
            if (useEvent)
            {
                shouldFinish = animationEventReceived || clipEnded || playingAnimationFallbackTimer <= 0f;
            }

            if (shouldFinish)
            {
                isWaitingAnimation = false;
            }

            return isWaitingAnimation;
        }

        /// <summary>
        /// Mục tiêu: dừng animator ở frame cuối của clip chết.
        /// Cách hoạt động: Play state tại normalizedTime = 1 và đặt speed = 0.
        /// </summary>
        public void FreezeDeathPose()
        {
            if (animator == null)
            {
                return;
            }

            int boolHash = deathConfig != null
                ? deathConfig.AnimatorBoolHash
                : AnimationConstants.IS_DYING;
            animator.SetBool(boolHash, true);

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            if (IsDeathAnimatorState(current))
            {
                animator.Play(current.shortNameHash, 0, 1f);
            }
            else if (TryResolveDeathStateHash(out int stateHash))
            {
                animator.Play(stateHash, 0, 1f);
            }

            animator.Update(0f);
            animator.speed = 0f;
        }

        /// <summary>
        /// Mục tiêu: bắt đầu giữ pose chết trước khi chìm.
        /// Cách hoạt động: reset timer; TickHoldDeathPose đọc HoldDeathPoseSeconds từ config.
        /// </summary>
        public void BeginHoldDeathPose()
        {
            phaseTimer = 0f;
            isHoldingPose = true;
        }

        /// <summary>
        /// Mục tiêu: giữ pose; trả true khi vẫn đang giữ.
        /// Cách hoạt động: cộng dồn timer đến khi đủ HoldDeathPoseSeconds.
        /// </summary>
        public bool TickHoldDeathPose(float deltaTime)
        {
            if (!isHoldingPose)
            {
                return false;
            }

            phaseTimer += deltaTime;
            if (phaseTimer >= GetHoldDeathPoseSeconds())
            {
                isHoldingPose = false;
            }

            return isHoldingPose;
        }

        /// <summary>
        /// Mục tiêu: bắt đầu chìm model xuống đất.
        /// Cách hoạt động: lưu vị trí bắt đầu/kết thúc theo SinkDepth từ config.
        /// </summary>
        public void BeginSink()
        {
            sinkStart = deathSinkTransform.position;
            sinkEnd = sinkStart + Vector3.down * GetSinkDepth();
            phaseTimer = 0f;
            isSinking = true;
        }

        /// <summary>
        /// Mục tiêu: cập nhật chìm; trả true khi vẫn đang chìm.
        /// Cách hoạt động: Lerp position theo SinkDuration; node Destroy riêng xóa object sau bước này.
        /// </summary>
        public bool TickSink(float deltaTime)
        {
            if (!isSinking)
            {
                return false;
            }

            float duration = Mathf.Max(0.05f, GetSinkDuration());
            phaseTimer += deltaTime;
            float t = Mathf.Clamp01(phaseTimer / duration);
            deathSinkTransform.position = Vector3.Lerp(sinkStart, sinkEnd, t);

            if (t >= 1f)
            {
                deathSinkTransform.position = sinkEnd;
                isSinking = false;
            }

            return isSinking;
        }

        public void NotifyDeathAnimationEvent()
        {
            animationEventReceived = true;
        }

        /// <summary>
        /// Mục tiêu: chạy toàn bộ chuỗi chết khi Behavior Graph chưa xử lý (fallback).
        /// Cách hoạt động: lặp các bước tương đương node graph; không Destroy — caller có thể destroy sau.
        /// </summary>
        public IEnumerator RunFallbackDeathSequence()
        {
            PrepareDeath();
            SetDeathAnimator();
            BeginWaitDeathAnimation();

            while (TickWaitDeathAnimation(Time.deltaTime))
            {
                yield return null;
            }

            FreezeDeathPose();
            BeginHoldDeathPose();

            while (TickHoldDeathPose(Time.deltaTime))
            {
                yield return null;
            }

            BeginSink();

            while (TickSink(Time.deltaTime))
            {
                yield return null;
            }
        }

        private void DisableGameplay()
        {
            if (agent != null)
            {
                agent.enabled = false;
            }

            DamageableSensor sensor = GetComponent<DamageableSensor>();
            if (sensor != null)
            {
                sensor.enabled = false;
            }

            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            Transform sink = deathSinkTransform != null ? deathSinkTransform : transform;
            foreach (Transform child in transform)
            {
                if (child == sink || child.IsChildOf(sink))
                {
                    continue;
                }

                if (sink.IsChildOf(child))
                {
                    continue;
                }

                child.gameObject.SetActive(false);
            }
        }

        private bool TryResolveDeathStateHash(out int stateHash)
        {
            stateHash = 0;
            if (animator == null)
            {
                return false;
            }

            if (deathConfig != null)
            {
                foreach (string stateName in deathConfig.GetDeathStateNamesToTry())
                {
                    int hash = Animator.StringToHash(stateName);
                    if (animator.HasState(0, hash))
                    {
                        stateHash = hash;
                        return true;
                    }
                }
            }

            foreach (string fallbackName in new[] { "Dying 0", "Dying" })
            {
                int hash = Animator.StringToHash(fallbackName);
                if (animator.HasState(0, hash))
                {
                    stateHash = hash;
                    return true;
                }
            }

            return false;
        }

        private bool IsDeathAnimatorState(AnimatorStateInfo state)
        {
            if (state.IsName("Dying 0") || state.IsName("Dying"))
            {
                return true;
            }

            return deathConfig != null && deathConfig.MatchesAnimatorState(state);
        }

        private float ResolveDeathClipLength()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return GetAnimationFallbackSeconds();
            }

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null)
                {
                    continue;
                }

                if (MatchesDeathClipName(clip.name))
                {
                    return clip.length;
                }
            }

            return GetAnimationFallbackSeconds();
        }

        private bool MatchesDeathClipName(string clipName)
        {
            if (deathConfig != null && deathConfig.MatchesClipName(clipName))
            {
                return true;
            }

            return clipName == "Dying"
                || clipName.StartsWith("Dying", System.StringComparison.Ordinal);
        }

        private float GetClipEndThreshold() =>
            deathConfig != null ? deathConfig.ClipEndNormalizedTime : 0.98f;

        private float GetHoldDeathPoseSeconds() =>
            deathConfig != null ? deathConfig.HoldDeathPoseSeconds : 0.35f;

        private float GetSinkDepth() =>
            deathConfig != null ? deathConfig.SinkDepth : 1.5f;

        private float GetSinkDuration() =>
            deathConfig != null ? deathConfig.SinkDuration : 1f;

        private float GetAnimationFallbackSeconds() =>
            deathConfig != null ? deathConfig.AnimationFallbackSeconds : 2f;

        private float GetAnimationMaxWaitSeconds() =>
            deathConfig != null ? deathConfig.AnimationMaxWaitSeconds : 4f;
    }
}

using System.Collections;
using GameDevTV.RTS.Units;
using Mirror;
using UnityEngine;

namespace GameDevTV.RTS.Netplay
{
    /// <summary>
    /// SRP: Đồng bộ máu / trạng thái chết cho entity MP — server authoritative.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class RtsUtsNetworkCombatSync : NetworkBehaviour
    {
        [SyncVar(hook = nameof(HookCurrentHealth))]
        int syncCurrentHealth;

        [SyncVar(hook = nameof(HookMaxHealth))]
        int syncMaxHealth;

        [SyncVar(hook = nameof(HookMarkedDead))]
        bool syncMarkedDead;

        AbstractCommandable _commandable;
        bool _applyingFromNetwork;

        void Awake()
        {
            _commandable = GetComponent<AbstractCommandable>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            PushFromCommandable();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!NetworkServer.active)
            {
                ApplyHealthFromNetwork();
                ApplyDeathFromNetwork();
            }
        }

        bool CanPushServerState() => RtsNetplayNetworkBehaviourUtility.CanPushServerState(this);

        /// <summary>
        /// Mục tiêu: Server/client routing cho TakeDamage trên entity networked.
        /// Cách hoạt động: Server trừ máu và push SyncVar; client thuần bỏ qua damage local.
        /// </summary>
        public void HandleTakeDamage(int damage, IDamageable attacker)
        {
            if (_commandable == null || damage <= 0 || _commandable.IsInDeathSequence)
            {
                return;
            }

            if (RtsNetplaySession.IsNetworkMatch && !NetworkServer.active)
            {
                return;
            }

            ApplyDamageLocally(damage, attacker);
            if (CanPushServerState())
            {
                PushFromCommandable();
            }
        }

        /// <summary>
        /// Mục tiêu: Đồng bộ chết/destroy giữa server và client.
        /// Cách hoạt động: Server đánh dấu dead + Destroy qua NetworkServer; client chạy Die presentation.
        /// </summary>
        public void HandleAuthoritativeDeath()
        {
            if (_commandable == null || _commandable.IsInDeathSequence)
            {
                return;
            }

            if (NetworkServer.active)
            {
                syncMarkedDead = true;

                if (_commandable is AbstractUnit)
                {
                    ExecuteDeathLocally();
                    StartCoroutine(ServerDestroyAfterUnitDeathDelay());
                    return;
                }

                if (_commandable is BaseBuilding)
                {
                    ExecuteDeathLocally();
                    if (netIdentity != null && netIdentity.netId != 0)
                    {
                        NetworkServer.Destroy(gameObject);
                    }
                    else
                    {
                        Destroy(gameObject);
                    }

                    return;
                }

                if (netIdentity != null && netIdentity.netId != 0)
                {
                    NetworkServer.Destroy(gameObject);
                }
                else
                {
                    Destroy(gameObject);
                }

                return;
            }

            if (!RtsNetplaySession.IsPureClient)
            {
                ExecuteDeathLocally();
            }
        }

        public void PushFromCommandable()
        {
            if (!CanPushServerState() || _commandable == null)
            {
                return;
            }

            syncCurrentHealth = _commandable.CurrentHealth;
            syncMaxHealth = _commandable.MaxHealth;
            syncMarkedDead = _commandable.IsInDeathSequence || _commandable.CurrentHealth <= 0;
        }

        public void PushHealFromCommandable()
        {
            PushFromCommandable();
        }

        void ApplyDamageLocally(int damage, IDamageable attacker)
        {
            if (_commandable.IsInDeathSequence)
            {
                return;
            }

            int lastHealth = _commandable.CurrentHealth;
            _commandable.ApplyAuthoritativeDamageCore(damage, attacker);
            _commandable.InvokeHealthUpdated(lastHealth, _commandable.CurrentHealth);

            if (_commandable.CurrentHealth <= 0)
            {
                HandleAuthoritativeDeath();
            }
        }

        void ExecuteDeathLocally()
        {
            if (_commandable is AbstractUnit unit)
            {
                unit.ExecuteNetworkDeathPresentation();
                return;
            }

            if (_commandable is BaseBuilding building)
            {
                building.ExecuteNetworkDeathPresentation();
                return;
            }

            Destroy(gameObject);
        }

        void HookCurrentHealth(int _, int __) => ApplyHealthFromNetwork();

        void HookMaxHealth(int _, int __) => ApplyHealthFromNetwork();

        void HookMarkedDead(bool _, bool __) => ApplyDeathFromNetwork();

        void ApplyHealthFromNetwork()
        {
            if (NetworkServer.active || _commandable == null || _applyingFromNetwork)
            {
                return;
            }

            _applyingFromNetwork = true;
            try
            {
                int last = _commandable.CurrentHealth;
                _commandable.ApplyNetworkHealthSnapshot(syncCurrentHealth, syncMaxHealth);
                _commandable.InvokeHealthUpdated(last, syncCurrentHealth);
            }
            finally
            {
                _applyingFromNetwork = false;
            }
        }

        void ApplyDeathFromNetwork()
        {
            if (NetworkServer.active || !syncMarkedDead || _commandable == null || _commandable.IsInDeathSequence)
            {
                return;
            }

            ExecuteDeathLocally();
        }

        IEnumerator ServerDestroyAfterUnitDeathDelay()
        {
            yield return new WaitForSeconds(2.5f);

            if (this == null || _commandable == null)
            {
                yield break;
            }

            if (netIdentity != null && netIdentity.netId != 0)
            {
                NetworkServer.Destroy(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}

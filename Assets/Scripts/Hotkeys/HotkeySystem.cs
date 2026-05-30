using System.Collections.Generic;
using UnityEngine;

namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// Entry point: gắn vào scene, tick HotkeyService mỗi frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HotkeySystem : MonoBehaviour
    {
        [SerializeField] HotkeyBindingCatalogSO bindingCatalog;
        [SerializeField] bool useBuiltInDefaultsIfEmpty = true;
        [Tooltip("Handler implement IHotkeyHandler (MonoBehaviour).")]
        [SerializeField] MonoBehaviour[] handlerComponents;
        [SerializeField] bool includeHandlersOnSameObject = true;
        [SerializeField] bool includeHandlersInChildren = true;

        HotkeyService service;
        UnityHotkeyInputSource inputSource;
        CompositeHotkeyGate gate;

        public HotkeyService Service => service;

        void Awake()
        {
            inputSource = new UnityHotkeyInputSource();
            gate = new CompositeHotkeyGate(
                new UiFocusHotkeyGate(),
                new LocalHumanHotkeyGate());

            var bindings = ResolveBindings();
            service = new HotkeyService(bindings, inputSource, gate);
            RegisterAllHandlers();
        }

        void Start()
        {
            RegisterAllHandlers();
        }

        void Update()
        {
            service?.Tick();
        }

        IReadOnlyList<HotkeyBindingEntry> ResolveBindings()
        {
            if (bindingCatalog != null && bindingCatalog.Bindings.Count > 0)
            {
                return bindingCatalog.Bindings;
            }

            return useBuiltInDefaultsIfEmpty
                ? HotkeyDefaults.CreateBindings()
                : System.Array.Empty<HotkeyBindingEntry>();
        }

        void RegisterAllHandlers()
        {
            if (handlerComponents != null)
            {
                for (int i = 0; i < handlerComponents.Length; i++)
                {
                    RegisterIfHandler(handlerComponents[i]);
                }
            }

            if (includeHandlersOnSameObject)
            {
                MonoBehaviour[] local = GetComponents<MonoBehaviour>();
                for (int i = 0; i < local.Length; i++)
                {
                    RegisterIfHandler(local[i]);
                }
            }

            if (includeHandlersInChildren)
            {
                MonoBehaviour[] children = GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    RegisterIfHandler(children[i]);
                }
            }
        }

        void RegisterIfHandler(MonoBehaviour component)
        {
            if (component is IHotkeyHandler handler)
            {
                service.Register(handler);
            }
        }
    }
}

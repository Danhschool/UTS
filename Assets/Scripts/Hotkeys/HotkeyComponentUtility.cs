using UnityEngine;

namespace GameDevTV.RTS.Hotkeys
{
    /// <summary>
    /// Tìm interface trên cùng GameObject (Unity không hỗ trợ GetComponent trực tiếp với interface).
    /// </summary>
    public static class HotkeyComponentUtility
    {
        public static TInterface FindInterfaceOnGameObject<TInterface>(GameObject gameObject)
            where TInterface : class
        {
            if (gameObject == null)
            {
                return null;
            }

            MonoBehaviour[] behaviours = gameObject.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is TInterface target)
                {
                    return target;
                }
            }

            return null;
        }
    }
}

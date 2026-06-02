using System.Collections;
using UnityEngine;

namespace GameDevTV.RTS.UI.Components
{
    /// <summary>
    /// SRP: Bật panel/dialog sau khi Awake/OnEnable lần đầu của hierarchy con đã chạy xong.
    /// </summary>
    public static class UiPanelActivation
    {
        /// <summary>
        /// Mục tiêu: Tránh phải bấm mở panel 2 lần khi object bắt đầu inactive.
        /// Cách hoạt động: Bật target ngay; coroutine chạy trên MonoBehaviour đang active (runner hoặc parent/target sau khi bật).
        /// </summary>
        public static void ShowDeferred(GameObject target, MonoBehaviour runner)
        {
            if (target == null)
            {
                return;
            }

            target.SetActive(true);

            MonoBehaviour host = ResolveActiveCoroutineHost(runner, target);
            if (host == null)
            {
                return;
            }

            host.StartCoroutine(ReassertActiveNextFrame(target));
        }

        static IEnumerator ReassertActiveNextFrame(GameObject target)
        {
            yield return null;

            if (target != null && !target.activeSelf)
            {
                target.SetActive(true);
            }
        }

        /// <summary>
        /// Mục tiêu: StartCoroutine chỉ trên object đang active (runner có thể nằm trên panel vừa tắt).
        /// Cách hoạt động: Ưu tiên runner nếu enabled; không thì parent active; cuối cùng component trên target đã bật.
        /// </summary>
        static MonoBehaviour ResolveActiveCoroutineHost(MonoBehaviour runner, GameObject target)
        {
            if (runner != null && runner.isActiveAndEnabled)
            {
                return runner;
            }

            if (runner != null)
            {
                Transform walk = runner.transform.parent;
                while (walk != null)
                {
                    if (walk.gameObject.activeInHierarchy)
                    {
                        MonoBehaviour[] behaviours = walk.GetComponents<MonoBehaviour>();
                        for (int i = 0; i < behaviours.Length; i++)
                        {
                            if (behaviours[i] != null && behaviours[i].isActiveAndEnabled)
                            {
                                return behaviours[i];
                            }
                        }
                    }

                    walk = walk.parent;
                }
            }

            if (target.activeInHierarchy)
            {
                MonoBehaviour[] onTarget = target.GetComponents<MonoBehaviour>();
                for (int i = 0; i < onTarget.Length; i++)
                {
                    if (onTarget[i] != null && onTarget[i].isActiveAndEnabled)
                    {
                        return onTarget[i];
                    }
                }
            }

            return null;
        }
    }
}

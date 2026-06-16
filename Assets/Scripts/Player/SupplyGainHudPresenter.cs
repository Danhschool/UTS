using System.Collections;
using GameDevTV.RTS.Netplay;
using GameDevTV.RTS.UI.GameEventLog;
using GameDevTV.RTS.Units;
using TMPro;
using UnityEngine;

namespace GameDevTV.RTS.Player
{
    /// <summary>
    /// SRP: Popup "+N Stone/Wood/Food" cạnh Supplies Bar khi gather (host offline + client MP qua Rpc).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SupplyGainHudPresenter : MonoBehaviour
    {
        const float FadeDurationSeconds = 1.35f;
        const float HoldDurationSeconds = 0.45f;
        const int MinAmountToShow = 1;
        const int MinAmountForEventLog = 25;

        static readonly Color StoneColor = new(0.69f, 0.69f, 0.69f, 1f);
        static readonly Color WoodColor = new(0.55f, 0.35f, 0.17f, 1f);
        static readonly Color FoodColor = new(0.9f, 0.66f, 0.09f, 1f);

        [SerializeField] Supplies supplies;
        [SerializeField] TextMeshProUGUI stoneAmountText;
        [SerializeField] TextMeshProUGUI woodAmountText;
        [SerializeField] TextMeshProUGUI foodAmountText;

        readonly GainSlot[] slots = new GainSlot[3];
        Owner hudOwner = Owner.Invalid;

        struct GainSlot
        {
            public TextMeshProUGUI Label;
            public int Accumulated;
            public Coroutine FadeRoutine;
        }

        void Awake()
        {
            supplies ??= GetComponent<Supplies>();
            if (supplies != null)
            {
                hudOwner = MpHudBranchResolver.ResolveFor(transform);
                if (hudOwner == Owner.Invalid)
                {
                    hudOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
                }
            }

            EnsureSlotsCreated();
        }

        /// <summary>
        /// Mục tiêu: Gắn text S/W/F từ Supplies sau khi HUD branch bind.
        /// Cách hoạt động: Lưu reference TMP làm anchor cho popup gain.
        /// </summary>
        public void BindSuppliesTexts(
            Owner owner,
            TextMeshProUGUI stoneText,
            TextMeshProUGUI woodText,
            TextMeshProUGUI foodAmount)
        {
            hudOwner = owner;
            stoneAmountText = stoneText;
            woodAmountText = woodText;
            foodAmountText = foodAmount;
            EnsureSlotsCreated();
        }

        /// <summary>
        /// Mục tiêu: Hiển thị +amount khi worker gather (host SupplyEvent hoặc client Rpc).
        /// Cách hoạt động: Cộng dồn nếu popup đang hiện; fade sau HoldDuration.
        /// </summary>
        public void NotifyGain(Owner owner, SupplyGainKind kind, int amount)
        {
            Owner localOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            if (amount < MinAmountToShow
                || kind == SupplyGainKind.Unknown
                || !HumanFogVisionUtility.EmitsFogVision(owner)
                || owner != localOwner)
            {
                return;
            }

            EnsureSlotsCreated();
            int index = (int)kind;
            if (index < 0 || index >= slots.Length || slots[index].Label == null)
            {
                return;
            }

            ref GainSlot slot = ref slots[index];
            slot.Accumulated += amount;
            slot.Label.text = $"+{slot.Accumulated}";
            slot.Label.color = GetColor(kind);
            slot.Label.gameObject.SetActive(true);

            if (slot.FadeRoutine != null)
            {
                StopCoroutine(slot.FadeRoutine);
            }

            slot.FadeRoutine = StartCoroutine(FadeOutAfterDelay(index));

            if (amount >= MinAmountForEventLog)
            {
                GameEventLog.Post(
                    $"Nhận +{amount} {GetDisplayName(kind)}.",
                    GameEventLogCategory.Resource);
            }
        }

        /// <summary>
        /// Mục tiêu: Client MP — GameMatchOverlayStateSync gọi khi server gather.
        /// </summary>
        public static void NotifyGainFromNetwork(Owner owner, SupplyGainKind kind, int amount)
        {
            if (!RtsNetplaySession.IsNetworkMatch || kind == SupplyGainKind.Unknown || amount <= 0)
            {
                return;
            }

            Owner localOwner = LocalHumanOwnerAccess.GetLocalOwnerOrDefault();
            if (owner != localOwner)
            {
                return;
            }

            Supplies hud = MpHudSuppliesResolver.FindForOwner(localOwner);
            if (hud == null)
            {
                return;
            }

            hud.PresentSupplyGain(kind, amount);
        }

        void EnsureSlotsCreated()
        {
            EnsureSlot(SupplyGainKind.Stone, stoneAmountText, 0);
            EnsureSlot(SupplyGainKind.Wood, woodAmountText, 1);
            EnsureSlot(SupplyGainKind.Food, foodAmountText, 2);
        }

        void EnsureSlot(SupplyGainKind kind, TextMeshProUGUI anchorText, int index)
        {
            if (slots[index].Label != null)
            {
                return;
            }

            if (anchorText == null)
            {
                return;
            }

            Transform parent = anchorText.transform.parent != null
                ? anchorText.transform.parent
                : anchorText.transform;

            GameObject go = new GameObject($"SupplyGain_{kind}");
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 8f);
            rect.sizeDelta = new Vector2(120f, 28f);

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.font = anchorText.font;
            label.fontSharedMaterial = anchorText.fontSharedMaterial;
            label.fontSize = Mathf.Max(18f, anchorText.fontSize * 0.85f);
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = string.Empty;
            go.SetActive(false);

            slots[index] = new GainSlot { Label = label };
        }

        IEnumerator FadeOutAfterDelay(int index)
        {
            yield return new WaitForSecondsRealtime(HoldDurationSeconds);

            GainSlot slot = slots[index];
            if (slot.Label == null)
            {
                yield break;
            }

            Color start = slot.Label.color;
            float elapsed = 0f;
            while (elapsed < FadeDurationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / FadeDurationSeconds);
                Color c = start;
                c.a = 1f - t;
                slot.Label.color = c;
                yield return null;
            }

            slot.Label.gameObject.SetActive(false);
            slot.Accumulated = 0;
            slot.Label.color = GetColor((SupplyGainKind)index);
            slots[index] = slot;
            slots[index].FadeRoutine = null;
        }

        static Color GetColor(SupplyGainKind kind) =>
            kind switch
            {
                SupplyGainKind.Stone => StoneColor,
                SupplyGainKind.Wood => WoodColor,
                SupplyGainKind.Food => FoodColor,
                _ => Color.white
            };

        static string GetDisplayName(SupplyGainKind kind) =>
            kind switch
            {
                SupplyGainKind.Stone => "Stone",
                SupplyGainKind.Wood => "Wood",
                SupplyGainKind.Food => "Food",
                _ => "Supply"
            };
    }
}

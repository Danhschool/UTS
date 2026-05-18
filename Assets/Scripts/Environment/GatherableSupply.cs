using System;
using GameDevTV.RTS.EventBus;
using GameDevTV.RTS.Events;
using GameDevTV.RTS.Player;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.Environment
{
    public class GatherableSupply : MonoBehaviour, IGatherable, IHideable
    {
        [field: SerializeField] public SupplySO Supply { get; private set; }
        [field: SerializeField] public int Amount { get; private set; }
        [field: SerializeField] public bool IsVisible { get; private set; }
        public Transform Transform => this == null ? null : transform;

        private Placeholder culledVisuals;
        private Renderer[] renderers = Array.Empty<Renderer>();
        private ParticleSystem[] particleSystems = Array.Empty<ParticleSystem>();

        public event IHideable.VisibilityChangeEvent OnVisibilityChanged;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            particleSystems = GetComponentsInChildren<ParticleSystem>();
        }

        private void Start()
        {
            Amount = Supply.MaxAmount;
            Bus<SupplySpawnEvent>.Raise(Owner.Unowned, new SupplySpawnEvent(this));
        }

        private void OnDestroy()
        {
            Bus<SupplyDepletedEvent>.Raise(Owner.Unowned, new SupplyDepletedEvent(this));
        }

        public void BeginGather()
        {
            // Nhiều unit có thể gather cùng lúc — không khóa node.
        }

        public int EndGather(int bonusPerGather = 0)
        {
            int perTrip = Mathf.Max(0, Supply.AmountPerGather + bonusPerGather);
            int amountGathered = Mathf.Min(perTrip, Amount);
            Amount -= amountGathered;

            if (Amount <= 0)
            {
                Destroy(gameObject);
            }

            return amountGathered;
        }

        public void AbortGather()
        {
            // Không còn trạng thái busy trên supply; giữ hook cho behavior graph / hủy gather.
        }

        public void SetVisible(bool isVisible)
        {
            if (isVisible == IsVisible) return;

            IsVisible = isVisible;
            OnVisibilityChanged?.Invoke(this, isVisible);

            if (IsVisible)
            {
                OnGainVisibility();
            }
            else
            {
                OnLoseVisibility();
            }
        }

        private void OnGainVisibility()
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }

            foreach (ParticleSystem particleSystem in particleSystems)
            {
                if (particleSystem != null)
                {
                    particleSystem.gameObject.SetActive(true);
                }
            }

            if (culledVisuals != null)
            {
                culledVisuals.gameObject.SetActive(false);
            }
        }

        private void OnLoseVisibility()
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }

            foreach (ParticleSystem particleSystem in particleSystems)
            {
                if (particleSystem != null)
                {
                    particleSystem.gameObject.SetActive(false);
                }
            }

            if (culledVisuals == null)
            {
                if (!TryCreateCulledVisuals())
                {
                    return;
                }
            }
            else
            {
                culledVisuals.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Mục tiêu: Tạo placeholder nhỏ khi supply bị che fog (chưa khám phá).
        /// Cách hoạt động: Chỉ dùng MeshRenderer tĩnh (cây/đá); bỏ qua SkinnedMesh (xác động vật) để tránh tư thế bind pose.
        /// </summary>
        private bool TryCreateCulledVisuals()
        {
            Renderer sourceRenderer = FindSourceRendererForCulledVisual();
            if (sourceRenderer == null)
            {
                return false;
            }

            Transform sourceTransform = sourceRenderer.transform;
            GameObject culledGO = new($"Culled {name} Visuals")
            {
                layer = LayerMask.NameToLayer("TransparentFX"),
                transform =
                {
                    position = sourceTransform.position,
                    rotation = sourceTransform.rotation,
                    localScale = sourceTransform.lossyScale
                }
            };

            culledVisuals = culledGO.AddComponent<Placeholder>();
            culledVisuals.ParentObject = gameObject;
            culledVisuals.Owner = Owner.Unowned;

            MeshFilter meshFilter = culledGO.AddComponent<MeshFilter>();
            MeshRenderer culledRenderer = culledGO.AddComponent<MeshRenderer>();

            if (sourceRenderer is not MeshRenderer meshRenderer)
            {
                Destroy(culledGO);
                culledVisuals = null;
                return false;
            }

            MeshFilter sourceFilter = meshRenderer.GetComponent<MeshFilter>();
            if (sourceFilter == null)
            {
                Destroy(culledGO);
                culledVisuals = null;
                return false;
            }

            meshFilter.sharedMesh = sourceFilter.sharedMesh;
            culledRenderer.sharedMaterials = meshRenderer.sharedMaterials;

            return true;
        }

        private Renderer FindSourceRendererForCulledVisual()
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer is MeshRenderer)
                {
                    return renderer;
                }
            }

            return GetComponentInChildren<MeshRenderer>(true);
        }
    }
}

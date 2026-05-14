using UnityEngine;

/// <summary>
/// Positions the move marker and plays looped animation until the instance is destroyed.
/// </summary>
public class MovementCursor : MonoBehaviour
{
    private Animation anim;
    private SkinnedMeshRenderer render;
    public float animationSpeed = 4;

    private void Start()
    {
        TryCacheRenderer();
        UpdateAnimSpeed();
    }

    /// <summary>
    /// Places the cursor at the goal, shows the mesh, and plays the animation in a loop until destroyed.
    /// </summary>
    public void AnimateOnPos(Vector3 pos, Color color)
    {
        if (!TryCacheRenderer())
        {
            return;
        }

        UpdateAnimSpeed();

        Transform pivot = transform.parent != null ? transform.parent : transform;
        pivot.position = pos;

        render.material.color = color;
        render.enabled = true;

        if (anim == null)
        {
            return;
        }

        AnimationState state = ResolvePrimaryAnimationState();
        if (state == null)
        {
            return;
        }

        state.wrapMode = WrapMode.Loop;
        state.speed = animationSpeed;
        anim.Rewind(state.name);
        anim.Play(state.name, PlayMode.StopAll);
    }

    /// <summary>
    /// Picks the first usable legacy Animation state: named "Play", else the default clip, else the first state in the component.
    /// </summary>
    private AnimationState ResolvePrimaryAnimationState()
    {
        if (anim["Play"] != null)
        {
            return anim["Play"];
        }

        if (anim.clip != null && anim[anim.clip.name] != null)
        {
            return anim[anim.clip.name];
        }

        foreach (AnimationState s in anim)
        {
            return s;
        }

        return null;
    }

    private bool TryCacheRenderer()
    {
        if (render == null)
        {
            render = GetComponent<SkinnedMeshRenderer>();
        }

        if (anim == null)
        {
            anim = GetComponentInParent<Animation>();
        }

        return render != null;
    }

    private void UpdateAnimSpeed()
    {
        if (anim == null)
        {
            return;
        }

        AnimationState state = ResolvePrimaryAnimationState();
        if (state != null)
        {
            state.speed = animationSpeed;
        }
    }
}

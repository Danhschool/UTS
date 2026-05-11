using UnityEngine;

namespace GameDevTV.RTS.Utilities
{
    public static class AnimationConstants
    {
        public static int IS_MOVING = Animator.StringToHash("isMoving");
        public static int IS_ENGAGING = Animator.StringToHash("isEngaging");
        public static int IS_ATTACK = Animator.StringToHash("isAttack");
        public static int IS_DYING = Animator.StringToHash("isDying");
        //public static int IS_CONSTRUCTION = Animator.StringToHash("isConstructing");

        public static int MOVING = Animator.StringToHash("isConstructing");
        public static int IDLE = Animator.StringToHash("isConstructing");
        public static int ENGAGING = Animator.StringToHash("isConstructing");
    }
}

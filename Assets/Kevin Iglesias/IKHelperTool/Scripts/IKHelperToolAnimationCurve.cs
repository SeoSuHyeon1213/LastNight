using UnityEngine;

namespace KevinIglesias 
{
    public enum IKHelperHandAnimationCurve { LeftHand = 0, RightHand = 1 }

    public class IKHelperToolAnimationCurve : MonoBehaviour
    {
        public Transform handEffector;
        public Transform poleTarget; // Elbow pole / hint target

        public string animationFloat;
        public IKHelperHandAnimationCurve hand;

        private Animator animator;
        private float weight;

        void Awake()
        {
            animator = GetComponent<Animator>();
            weight = 0f;
        }

        void Update()
        {
            weight = Mathf.Lerp(0, 1, animator.GetFloat(animationFloat));
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (handEffector == null) return;

            AvatarIKGoal goal;
            AvatarIKHint hint;

            if (hand == IKHelperHandAnimationCurve.LeftHand)
            {
                goal = AvatarIKGoal.LeftHand;
                hint = AvatarIKHint.LeftElbow;
            }
            else
            {
                goal = AvatarIKGoal.RightHand;
                hint = AvatarIKHint.RightElbow;
            }

            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKPosition(goal, handEffector.position);

            animator.SetIKRotationWeight(goal, weight);
            animator.SetIKRotation(goal, handEffector.rotation);

            if (poleTarget != null)
            {
                animator.SetIKHintPositionWeight(hint, weight);
                animator.SetIKHintPosition(hint, poleTarget.position);
            }
        }
    }
}
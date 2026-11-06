using UnityEngine;

namespace AdvancedController
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimator : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;

        private Animator animator;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        [SerializeField] private float dampTime = 0.1f;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            if (playerController == null || animator == null) return;

            float currentSpeed = playerController.GetMovementVelocity().magnitude;

            float normalizedSpeed = currentSpeed / playerController.movementSpeed;

            animator.SetFloat(SpeedHash, normalizedSpeed, dampTime, Time.deltaTime);
        }
    }
}
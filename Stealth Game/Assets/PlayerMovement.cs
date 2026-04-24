using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float smoothInputSpeed = 4f;
    public Animator animator;

    private Rigidbody rb;
    private Vector3 currentInput;
    private Vector3 smoothMoveDirection;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        currentInput = new Vector3(moveX, 0f, moveZ).normalized;

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool("isMoving", currentInput.sqrMagnitude > 0.01f);
        }
    }

    void FixedUpdate()
    {
        smoothMoveDirection = Vector3.Lerp(
            smoothMoveDirection,
            currentInput,
            smoothInputSpeed * Time.fixedDeltaTime
        );

        Vector3 newPosition = rb.position + smoothMoveDirection * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(newPosition);

        if (smoothMoveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(smoothMoveDirection);
            Quaternion smoothRotation = Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            );

            rb.MoveRotation(smoothRotation);
        }
    }
}
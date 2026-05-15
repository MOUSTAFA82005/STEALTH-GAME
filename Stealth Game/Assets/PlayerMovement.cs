using UnityEngine; // Unity framework

public class PlayerMovement : MonoBehaviour // Handles player movement
{
    public float    moveSpeed       = 5f;   // Units/s // Movement speed
    public float    rotationSpeed   = 10f;  // Slerp factor // Rotation speed
    public float    smoothInputSpeed = 4f;  // Input lerp // Input smoothing
    public Animator animator;               // Player animator // Animator reference

    private Rigidbody rb; // Rigidbody component
    private Vector3   currentInput;         // Raw WASD // Raw input
    private Vector3   smoothMoveDirection;  // Lerped direction // Smoothed direction

    void Start() // Initialize
    {
        rb = GetComponent<Rigidbody>(); // Cache rigidbody
    }

    void Update() // Per-frame input
    {
        // read input // Read player input
        float moveX = Input.GetAxisRaw("Horizontal"); // Horizontal axis
        float moveZ = Input.GetAxisRaw("Vertical"); // Vertical axis
        currentInput = new Vector3(moveX, 0f, moveZ).normalized; // Normalize input

        // animate // Update animation
        if (animator != null && animator.runtimeAnimatorController != null) // Has animator
            animator.SetBool("isMoving", currentInput.sqrMagnitude > 0.01f); // Set moving state
    }

    void FixedUpdate() // Physics update
    {
        // smooth // Smooth input
        smoothMoveDirection = Vector3.Lerp(smoothMoveDirection, currentInput, smoothInputSpeed * Time.fixedDeltaTime); // Interpolate

        // move // Move player
        rb.MovePosition(rb.position + smoothMoveDirection * moveSpeed * Time.fixedDeltaTime); // Apply movement

        // rotate // Rotate player
        if (smoothMoveDirection.sqrMagnitude > 0.001f) // Has movement
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(smoothMoveDirection), rotationSpeed * Time.fixedDeltaTime)); // Face direction
    }
}

using System.Collections; // Coroutines
using UnityEngine; // Unity framework

public class PlayerTakedown : MonoBehaviour // Handles player takedown mechanic
{
    public float    takedownRange        = 2f;   // Detection radius // Radius
    public float    fallDuration         = 0.4f; // Seconds to fall // Duration
    public float    fallDownOffset       = 0.5f; // Units to sink // Offset
    public float    waitBeforeDisappear  = 0.5f; // Seconds before shrink // Wait
    public float    disappearDuration    = 0.5f; // Seconds to shrink // Duration
    public Animator playerAnimator;              // Player animator reference // Reference
    public float    attackSpeed          = 3f;   // Animation multiplier // Speed multiplier

    void Update() // Per-frame input check
    {
        // input // Check for input
        if (Input.GetKeyDown(KeyCode.E)) // E key pressed
            TryTakedownGuard(); // Attempt takedown
    }

    void TryTakedownGuard() // Attempt takedown on nearby guard
    {
        // scan // Scan for guards
        Collider[] hits = Physics.OverlapSphere(transform.position, takedownRange); // Get nearby colliders
        foreach (Collider hit in hits) // For each hit
        {
            if (!hit.CompareTag("Guard")) continue; // Skip non-guards

            GuardAI guardAI = hit.GetComponent<GuardAI>(); // Get guard AI
            if (guardAI != null && guardAI.isDead) return; // Already dead

            // animate // Trigger attack animation
            if (playerAnimator != null) // Has animator
            {
                playerAnimator.speed = attackSpeed; // Speed up animation
                playerAnimator.SetTrigger("attack"); // Trigger attack
                StartCoroutine(ResetSpeed()); // Schedule speed reset
            }

            // execute // Execute takedown
            ExecuteTakedown(hit); // Perform takedown
            break; // Only one guard
        }
    }

    IEnumerator ResetSpeed() // Reset animation speed after delay
    {
        yield return new WaitForSeconds(0.25f); // Wait 0.25 seconds
        playerAnimator.speed = 1f; // Reset to normal
    }

    void ExecuteTakedown(Collider hit) // Execute takedown on guard
    {
        // get components // Cache guard components
        GuardAI         guardAI  = hit.GetComponent<GuardAI>(); // Guard AI
        Rigidbody       guardRb  = hit.GetComponent<Rigidbody>(); // Rigidbody
        CapsuleCollider guardCol = hit.GetComponent<CapsuleCollider>(); // Collider
        Animator        guardAnim = hit.GetComponentInChildren<Animator>(); // Animator

        // kill // Mark as dead
        if (guardAI != null) // Has AI
        {
            guardAI.isDead = true; // Set dead flag
            KillCounter.Instance?.AddKill(); // Increment kill counter
        }

        // stop anim // Stop guard animation
        if (guardAnim != null) // Has animator
            guardAnim.SetBool("isRunning", false); // Stop running

        // freeze physics // Disable physics
        if (guardRb != null) // Has rigidbody
        {
            guardRb.linearVelocity  = Vector3.zero; // Stop movement
            guardRb.angularVelocity = Vector3.zero; // Stop rotation
            guardRb.isKinematic     = true; // Make kinematic
        }

        // disable collision // Turn off collider
        if (guardCol != null) // Has collider
            guardCol.enabled = false; // Disable collider

        StartCoroutine(FallAndDisappear(hit.transform)); // Start disappear animation
    }

    IEnumerator FallAndDisappear(Transform guard) // Animate guard falling and disappearing
    {
        // fall // Fall animation
        Quaternion startRot = guard.rotation; // Starting rotation
        Quaternion endRot   = Quaternion.Euler(0f, guard.eulerAngles.y, 90f); // Ending rotation
        Vector3    startPos = guard.position; // Starting position
        Vector3    endPos   = startPos + Vector3.down * fallDownOffset; // Ending position
        float time = 0f; // Elapsed time

        while (time < fallDuration) // While falling
        {
            time += Time.deltaTime; // Increment time
            float t = time / fallDuration; // Normalized time
            guard.rotation = Quaternion.Slerp(startRot, endRot, t); // Lerp rotation
            guard.position = Vector3.Lerp(startPos, endPos, t); // Lerp position
            yield return null; // Wait one frame
        }
        guard.rotation = endRot; // Set final rotation
        guard.position = endPos; // Set final position

        yield return new WaitForSeconds(waitBeforeDisappear); // Wait before disappearing

        // shrink // Shrink animation
        Vector3 startScale = guard.localScale; // Starting scale
        time = 0f; // Reset time
        while (time < disappearDuration) // While shrinking
        {
            time += Time.deltaTime; // Increment time
            guard.localScale = Vector3.Lerp(startScale, Vector3.zero, time / disappearDuration); // Lerp scale
            yield return null; // Wait one frame
        }
        guard.localScale = Vector3.zero; // Set to zero scale
        guard.gameObject.SetActive(false); // Deactivate object
    }

#if UNITY_EDITOR // Editor only
    void OnDrawGizmosSelected() // Draw debug gizmos
    {
        Gizmos.color = Color.red; // Red color
        Gizmos.DrawWireSphere(transform.position, takedownRange); // Draw radius
    }
#endif // End editor
}

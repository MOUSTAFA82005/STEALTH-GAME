using UnityEngine;

// CameraFollow: Makes the camera smoothly follow the player from a fixed angle.
// Attach this script to the Main Camera in the scene.
public class CameraFollow : MonoBehaviour
{
    // The object the camera will follow (drag the Player here in the Inspector)
    public Transform target;

    // How far the camera sits from the player (X = side, Y = height, Z = depth behind)
    public Vector3 offset = new Vector3(0f, 15f, -10f);

    // How quickly the camera catches up to the player (higher = snappier)
    public float smoothSpeed = 2f;

    // The fixed rotation angle of the camera (55 degrees tilts it like a top-down view)
    public Vector3 fixedRotation = new Vector3(55f, 0f, 0f);

    // LateUpdate runs after all other updates, so the camera moves AFTER the player moves
    // This prevents the camera from lagging one frame behind the player
    void LateUpdate()
    {
        // Safety check: if there is no target assigned, do nothing
        if (target == null) return;

        // Calculate where the camera should ideally be (player position + the offset distance)
        Vector3 desiredPosition = target.position + offset;

        // Smoothly move the camera toward the desired position using linear interpolation (Lerp)
        // Lerp blends between current position and desired position by smoothSpeed each frame
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // Lock the camera rotation so it never tilts or spins, always stays at the fixed angle
        transform.rotation = Quaternion.Euler(fixedRotation);
    }
}

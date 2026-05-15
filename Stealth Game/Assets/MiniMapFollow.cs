using UnityEngine; // Unity framework

public class MiniMapFollow : MonoBehaviour // Handles minimap camera follow
{
    public Transform target;        // Player // Target to follow
    public float     height = 25f;  // Above player // Camera height

    void LateUpdate() // Update after all objects moved
    {
        if (target == null) return; // No target

        // top-down follow // Top-down camera
        transform.position = new Vector3(target.position.x, target.position.y + height, target.position.z); // Position above player
        transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Rotate to top-down
    }
}

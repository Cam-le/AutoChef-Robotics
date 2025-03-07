using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float panSpeed = 20f;
    public float zoomSpeed = 10f;
    public float rotationSpeed = 100f;

    [Header("Constraints")]
    public float minZoomDistance = 0.5f;
    public float maxZoomDistance = 10f;
    public float minVerticalAngle = -80f;
    public float maxVerticalAngle = 80f;

    private Vector3 targetPosition;
    private float currentZoomDistance;
    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        // Set initial position and rotation
        targetPosition = new Vector3(0, 0.45f, 0.6f); // Center of the scene or robot position

        // Calculate initial distances and angles
        Vector3 direction = transform.position - targetPosition;
        currentZoomDistance = direction.magnitude;

        rotationY = transform.eulerAngles.y;
        rotationX = transform.eulerAngles.x;

        // Ensure we're looking at the target
        transform.LookAt(targetPosition);
    }

    void LateUpdate()
    {
        // Only process input if right mouse button is held down
        if (Input.GetMouseButton(1))
        {
            // Rotation - Mouse movement
            rotationY += Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            rotationX -= Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;

            // Clamp vertical rotation
            rotationX = Mathf.Clamp(rotationX, minVerticalAngle, maxVerticalAngle);

            // Zoom - Mouse scroll wheel
            float zoomDelta = Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
            currentZoomDistance = Mathf.Clamp(currentZoomDistance - zoomDelta, minZoomDistance, maxZoomDistance);

            // Panning - WASD or arrow keys
            float horizontalInput = Input.GetAxis("Horizontal");
            float verticalInput = Input.GetAxis("Vertical");

            if (horizontalInput != 0 || verticalInput != 0)
            {
                // Calculate panning direction in world space
                Vector3 right = transform.right * horizontalInput;
                Vector3 forward = transform.forward * verticalInput;
                forward.y = 0; // Keep panning parallel to the ground
                Vector3 panDirection = (right + forward).normalized;

                // Apply panning
                targetPosition += panDirection * panSpeed * Time.deltaTime;
            }
        }

        // Calculate the new camera position
        Quaternion rotation = Quaternion.Euler(rotationX, rotationY, 0);
        Vector3 negativeDistance = new Vector3(0, 0, -currentZoomDistance);
        Vector3 position = rotation * negativeDistance + targetPosition;

        // Apply the position and rotation
        transform.rotation = rotation;
        transform.position = position;
    }
}
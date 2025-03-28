using UnityEngine;


public class CameraController : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform targetToFollow; // Optional: Assign a target object to follow
    public Vector3 targetOffset = Vector3.zero; // Offset from the target transform's position

    [Header("Movement Settings")]
    public float panSpeed = 20f;        // Speed for keyboard panning (moves the target)
    public float orbitSpeed = 150f;     // Speed for RMB rotation
    public float zoomSpeed = 15f;       // Speed for scroll wheel zoom
    public float screenPanSensitivity = 5f; // Sensitivity for MMB screen panning

    [Header("Constraints")]
    public float minZoomDistance = 1f;
    public float maxZoomDistance = 20f;
    public float minVerticalAngle = -85f;
    public float maxVerticalAngle = 85f;
    public bool clampGroundPlane = true; // Prevent target from going below y=0 during panning

    [Header("Smoothing")]
    public bool enableSmoothing = true;
    public float moveSmoothTime = 0.1f;
    public float rotateSmoothTime = 0.08f;
    public float zoomSmoothTime = 0.15f;

    // --- Private Variables ---
    private Vector3 _targetPosition;
    private Vector3 _smoothedTargetPosition;
    private Vector3 _positionVelocity = Vector3.zero; // For SmoothDamp position

    private float _desiredZoomDistance;
    private float _currentZoomDistance;
    private float _zoomVelocity = 0f; // For SmoothDamp zoom

    private float _rotationX; // Vertical angle
    private float _rotationY; // Horizontal angle
    private float _smoothedRotationX;
    private float _smoothedRotationY;
    private float _rotXVelocity = 0f; // For SmoothDamp rotationX
    private float _rotYVelocity = 0f; // For SmoothDamp rotationY

    private Vector3 _lastMousePosition; // For drag calculations

    // Initial state storage for reset
    private Vector3 _initialTargetPosition;
    private float _initialZoomDistance;
    private float _initialRotationX;
    private float _initialRotationY;
    private bool _isInitialStateStored = false;


    void Start()
    {
        // Initial setup based on Inspector values or current scene placement
        if (targetToFollow != null)
        {
            _targetPosition = targetToFollow.position + targetOffset;
        }
        else if (targetOffset != Vector3.zero) // If no target, use offset as initial focus point relative to origin
        {
            _targetPosition = targetOffset;
        }
        else // Default fallback if nothing else set
        {
            _targetPosition = Vector3.zero;
        }


        Vector3 initialDirection = transform.position - _targetPosition;
        _desiredZoomDistance = initialDirection.magnitude;
        _currentZoomDistance = _desiredZoomDistance;

        // Calculate initial rotation angles based on direction
        // Note: Quaternion.LookRotation gives rotation TO the target. We want rotation FROM target.
        Quaternion initialRotation = Quaternion.LookRotation(-initialDirection.normalized, Vector3.up);
        _rotationX = initialRotation.eulerAngles.x;
        _rotationY = initialRotation.eulerAngles.y;

        // Adjust angle representation if needed (Euler angles can wrap weirdly)
        if (_rotationX > 180f) _rotationX -= 360f;
        _rotationX = Mathf.Clamp(_rotationX, minVerticalAngle, maxVerticalAngle); // Clamp initial angle

        _smoothedTargetPosition = _targetPosition;
        _smoothedRotationX = _rotationX;
        _smoothedRotationY = _rotationY;

        // Ensure camera looks at the target initially
        ApplyCameraTransform(false); // Apply directly without smoothing

        StoreInitialState();
    }

    void StoreInitialState()
    {
        _initialTargetPosition = _targetPosition;
        _initialZoomDistance = _desiredZoomDistance;
        _initialRotationX = _rotationX;
        _initialRotationY = _rotationY;
        _isInitialStateStored = true;
    }

    void LateUpdate()
    {
        if (!_isInitialStateStored) StoreInitialState(); // Store initial state if not done yet (e.g., if Start was skipped)

        // --- Handle Input ---
        HandleTargetFollow();
        HandleZoomInput();
        HandleRotationInput();
        HandlePanningInput();
        HandleResetInput();

        // --- Apply Smoothing (if enabled) ---
        ApplySmoothing();

        // --- Calculate and Apply Final Transform ---
        ApplyCameraTransform(enableSmoothing);
    }

    void HandleTargetFollow()
    {
        if (targetToFollow != null)
        {
            // Update desired target position if following a transform
            _targetPosition = targetToFollow.position + targetOffset;
        }
    }

    void HandleZoomInput()
    {
        float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollDelta) > 0.01f)
        {
            _desiredZoomDistance -= scrollDelta * zoomSpeed * (_desiredZoomDistance / maxZoomDistance * 0.5f + 0.5f); // Make zoom faster when further away
            _desiredZoomDistance = Mathf.Clamp(_desiredZoomDistance, minZoomDistance, maxZoomDistance);
        }
    }

    void HandleRotationInput()
    {
        // Orbiting (Right Mouse Button)
        if (Input.GetMouseButtonDown(1))
        {
            _lastMousePosition = Input.mousePosition; // Store starting position for drag
        }
        if (Input.GetMouseButton(1))
        {
            Vector3 mouseDelta = Input.mousePosition - _lastMousePosition;

            // Calculate rotation changes (invert Y-axis movement)
            _rotationY += mouseDelta.x * orbitSpeed * Time.deltaTime;
            _rotationX -= mouseDelta.y * orbitSpeed * Time.deltaTime;

            // Clamp vertical rotation
            _rotationX = Mathf.Clamp(_rotationX, minVerticalAngle, maxVerticalAngle);

            _lastMousePosition = Input.mousePosition; // Update last position for next frame
        }
    }

    void HandlePanningInput()
    {
        // --- Keyboard Panning (Moves Target) ---
        float horizontalInput = Input.GetAxis("Horizontal"); // A/D or Left/Right Arrows
        float verticalInput = Input.GetAxis("Vertical");   // W/S or Up/Down Arrows

        if (Mathf.Abs(horizontalInput) > 0.01f || Mathf.Abs(verticalInput) > 0.01f)
        {
            if (targetToFollow == null) // Only allow keyboard panning if not following a target
            {
                // Calculate pan direction based on camera's orientation but parallel to the ground
                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();

                Vector3 right = transform.right;
                right.y = 0;
                right.Normalize();

                Vector3 panDirection = (right * horizontalInput + forward * verticalInput).normalized;
                float currentPanSpeed = panSpeed * (_currentZoomDistance / maxZoomDistance * 0.7f + 0.3f); // Pan faster when zoomed out

                _targetPosition += panDirection * currentPanSpeed * Time.deltaTime;

                // Optional: Clamp target position to ground plane
                if (clampGroundPlane && _targetPosition.y < 0f)
                {
                    _targetPosition.y = 0f;
                }
            }
        }

        // --- Screen Panning (Middle Mouse Button - Moves Camera & Target together) ---
        if (Input.GetMouseButtonDown(2))
        {
            _lastMousePosition = Input.mousePosition; // Store starting position for drag
        }
        if (Input.GetMouseButton(2))
        {
            Vector3 mouseDelta = Input.mousePosition - _lastMousePosition;

            // Calculate movement in screen space and convert to world space offset
            // Adjust sensitivity based on distance to make panning feel consistent
            float sensitivityScale = _currentZoomDistance * (screenPanSensitivity / 1000f); // Adjust 1000f if needed

            Vector3 move = (transform.right * -mouseDelta.x + transform.up * -mouseDelta.y) * sensitivityScale;

            _targetPosition += move; // Move the target position by the calculated offset

            _lastMousePosition = Input.mousePosition; // Update last position
        }
    }

    void HandleResetInput()
    {
        // Reset View (F key)
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (_isInitialStateStored)
            {
                // Instantly snap target if not following, otherwise just reset rotation/zoom
                if (targetToFollow == null)
                {
                    _targetPosition = _initialTargetPosition;
                    _smoothedTargetPosition = _targetPosition; // Avoid smooth damp fighting the reset
                }

                _desiredZoomDistance = _initialZoomDistance;
                _rotationX = _initialRotationX;
                _rotationY = _initialRotationY;

                // Also reset smoothed values to avoid lerp/damp fighting the reset
                _currentZoomDistance = _desiredZoomDistance;
                _smoothedRotationX = _rotationX;
                _smoothedRotationY = _rotationY;
            }
        }
    }


    void ApplySmoothing()
    {
        if (enableSmoothing)
        {
            _smoothedTargetPosition = Vector3.SmoothDamp(_smoothedTargetPosition, _targetPosition, ref _positionVelocity, moveSmoothTime);
            _currentZoomDistance = Mathf.SmoothDamp(_currentZoomDistance, _desiredZoomDistance, ref _zoomVelocity, zoomSmoothTime);
            _smoothedRotationX = Mathf.SmoothDampAngle(_smoothedRotationX, _rotationX, ref _rotXVelocity, rotateSmoothTime);
            _smoothedRotationY = Mathf.SmoothDampAngle(_smoothedRotationY, _rotationY, ref _rotYVelocity, rotateSmoothTime);
        }
        else // If smoothing is disabled, use target values directly
        {
            _smoothedTargetPosition = _targetPosition;
            _currentZoomDistance = _desiredZoomDistance;
            _smoothedRotationX = _rotationX;
            _smoothedRotationY = _rotationY;
        }
    }

    void ApplyCameraTransform(bool useSmoothedValues)
    {
        float R_X = useSmoothedValues ? _smoothedRotationX : _rotationX;
        float R_Y = useSmoothedValues ? _smoothedRotationY : _rotationY;
        float zoom = useSmoothedValues ? _currentZoomDistance : _desiredZoomDistance;
        Vector3 targetPos = useSmoothedValues ? _smoothedTargetPosition : _targetPosition;

        // Calculate rotation as Quaternion
        Quaternion rotation = Quaternion.Euler(R_X, R_Y, 0);

        // Calculate position based on rotation, zoom distance, and target position
        Vector3 direction = rotation * Vector3.forward; // Direction camera should face
        Vector3 position = targetPos - direction * zoom; // Position camera behind target

        // Apply the final position and rotation
        transform.position = position;
        transform.rotation = rotation;

        // Sanity check: Ensure camera is looking towards the target after calculations
        // transform.LookAt(targetPos); // Can uncomment this for debugging, but the math above should handle it.
    }
}


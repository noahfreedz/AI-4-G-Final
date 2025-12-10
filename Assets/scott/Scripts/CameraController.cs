using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 2f;
    [SerializeField] private float verticalSpeed = 3f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 10f;

    [Header("Mouse Look Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float minVerticalAngle = -90f;
    [SerializeField] private float maxVerticalAngle = 90f;

    private float rotationX = 0f;
    private float rotationY = 0f;
    private Vector3 currentVelocity = Vector3.zero;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Vector3 currentRotation = transform.eulerAngles;
        rotationY = currentRotation.y;
        rotationX = currentRotation.x;

        if (rotationX > 180f)
            rotationX -= 360f;
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleCursorToggle();
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        if (invertY)
            rotationX += mouseY;
        else
            rotationX -= mouseY;

        rotationX = Mathf.Clamp(rotationX, minVerticalAngle, maxVerticalAngle);

        rotationY += mouseX;

        transform.eulerAngles = new Vector3(rotationX, rotationY, 0f);
    }

    void HandleMovement()
    {
      
        float horizontal = Input.GetAxisRaw("Horizontal"); 
        float vertical = Input.GetAxisRaw("Vertical");  

        bool isSprinting = Input.GetKey(KeyCode.LeftShift);
        float currentSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 desiredDirection = (forward * vertical + right * horizontal);
        if (desiredDirection.sqrMagnitude > 1f)
            desiredDirection.Normalize();

        float verticalInput = 0f;
        if (Input.GetKey(KeyCode.E))
            verticalInput = 1f;
        else if (Input.GetKey(KeyCode.Q))
            verticalInput = -1f;

        Vector3 targetVelocity = desiredDirection * currentSpeed;
        targetVelocity.y = verticalInput * verticalSpeed;

       
        float accelRate = desiredDirection.sqrMagnitude > 0 || verticalInput != 0 ? acceleration : deceleration;
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, accelRate * Time.deltaTime);

        transform.position += currentVelocity * Time.deltaTime;
    }

    void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
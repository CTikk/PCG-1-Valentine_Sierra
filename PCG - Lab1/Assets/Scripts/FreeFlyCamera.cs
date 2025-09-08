using UnityEngine;

[DisallowMultipleComponent]
public class FreeFlyCamera : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 10f;
    public float shiftMultiplier = 3f;
    public float climbSpeed = 6f; // Q/E
    public float scrollSpeedScale = 1.15f; // rueda del mouse para subir/bajar velocidad

    [Header("Look (RMB)")]
    public float mouseSensitivity = 2.0f;
    public float pitchMin = -89f;
    public float pitchMax = 89f;

    [Header("Extras")]
    public bool holdRightMouseToLook = true;
    public bool lockCursorWhileLooking = true;

    float yaw;
    float pitch;

    void Start()
    {
        var e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x;
    }

    void Update()
    {
        // rueda = ajustar velocidad
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            if (scroll > 0) moveSpeed *= scrollSpeedScale;
            else moveSpeed /= scrollSpeedScale;
            moveSpeed = Mathf.Clamp(moveSpeed, 0.5f, 200f);
        }

        bool looking = !holdRightMouseToLook || Input.GetMouseButton(1);
        if (looking)
        {
            float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = -Input.GetAxis("Mouse Y") * mouseSensitivity;
            yaw += mx;
            pitch = Mathf.Clamp(pitch + my, pitchMin, pitchMax);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            if (lockCursorWhileLooking)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        else if (lockCursorWhileLooking)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // WASD + QE
        Vector3 dir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) dir += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) dir += Vector3.back;
        if (Input.GetKey(KeyCode.A)) dir += Vector3.left;
        if (Input.GetKey(KeyCode.D)) dir += Vector3.right;
        if (Input.GetKey(KeyCode.E)) dir += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) dir += Vector3.down;

        if (dir.sqrMagnitude > 0f)
        {
            float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? shiftMultiplier : 1f);
            transform.position += transform.TransformDirection(dir.normalized) * speed * Time.unscaledDeltaTime;
        }
    }
}
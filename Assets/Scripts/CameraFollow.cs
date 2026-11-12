
using UnityEngine;
using UnityEngine.InputSystem;

public class GolfCameraController : MonoBehaviour
{
    [Header("Objetivo")]
    public Transform target;
    public float targetHeight = 0f;

    [Header("Rotación")]
    public float rotationSpeed = 0.15f;
    public float initialPitch = 25f;

    [Header("Zoom")]
    public float distance = 8f;
    public float minDistance = 3f;
    public float maxDistance = 15f;
    public float zoomSpeed = 0.02f;

    private float yaw;
    private float pitch;

    void Start()
    {
        yaw = transform.eulerAngles.y;
        pitch = initialPitch;
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (Mouse.current == null) return;

        // Rotación con botón derecho
        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            yaw += mouseDelta.x * rotationSpeed;
        }

        // Zoom con rueda del ratón
        float scroll = Mouse.current.scroll.ReadValue().y;

        distance -= scroll * zoomSpeed;

        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        // Mantener la pelota en el centro
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 targetPosition = target.position +
                                 Vector3.up * targetHeight;

        Vector3 cameraPosition = targetPosition -
                                 rotation * Vector3.forward * distance;

        transform.position = cameraPosition;
        transform.rotation = rotation;
    }
}

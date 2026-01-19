using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple camera controller for viewing the DOTS prototype
/// Uses traditional MonoBehaviour since it's just for the editor/viewing
/// </summary>
public class PrototypeCameraController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 20f;
    public float zoomSpeed = 10f;
    
    [Header("Zoom Limits")]
    public float minZoom = 5f;
    public float maxZoom = 100f;
    
    private Camera cam;
    
    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
            cam = Camera.main;
    }
    
    void Update()
    {
        HandleMovement();
        HandleZoom();
    }
    
    void HandleMovement()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        
        Vector3 movement = Vector3.zero;
        
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            movement.y += 1;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            movement.y -= 1;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            movement.x -= 1;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            movement.x += 1;
        
        // Scale speed with zoom level
        float zoomFactor = cam != null ? cam.orthographicSize / 10f : 1f;
        transform.position += movement.normalized * moveSpeed * zoomFactor * Time.deltaTime;
    }
    
    void HandleZoom()
    {
        var mouse = Mouse.current;
        if (mouse == null || cam == null) return;
        
        float scroll = mouse.scroll.ReadValue().y;
        
        if (scroll != 0)
        {
            cam.orthographicSize -= scroll * zoomSpeed * Time.deltaTime;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
        }
    }
}

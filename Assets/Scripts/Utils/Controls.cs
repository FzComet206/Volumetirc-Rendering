using UnityEngine;
using UnityEngine.InputSystem;

public class Controls : MonoBehaviour
{
    private GameObject sceneUI;
    
    
    
    // Start is called before the first frame update
    [SerializeField] InputAction cruise;
    [SerializeField] InputAction mouse;

    [SerializeField] private Rigidbody rb;
    public int cruiseSpeed;
    public int rotateSpeed;

    private float xRotation;
    private float yRotation;

    private void Start()
    {
        sceneUI = Resources.FindObjectsOfTypeAll<SceneUI>()[0].gameObject;
    }

    private void Update()
    {
        if (sceneUI.activeSelf)
        {
            return;
        }
        // movement
        Vector3 c = cruise.ReadValue<Vector3>();
        Vector3 forward = c.z * transform.forward;
        Vector3 right = c.x * transform.right;
        Vector3 up = c.y * transform.up;
        Vector3 vel = (forward + up + right).normalized * (cruiseSpeed * Time.deltaTime);
        rb.velocity = vel;

        // rotation
        Vector2 delta = mouse.ReadValue<Vector2>();
        float rx = delta.y * rotateSpeed * Time.fixedDeltaTime;
        float ry = delta.x * rotateSpeed * Time.fixedDeltaTime;
        xRotation -= rx;
        xRotation = Mathf.Clamp(xRotation, -90, 90);
        yRotation += ry;
        transform.rotation = Quaternion.Euler(xRotation, yRotation, 0f).normalized;
    }

    private void OnEnable()
    {
        Cursor.visible = false;
        cruise.Enable();
        mouse.Enable();
    }
    
    private void OnDisable()
    {
        Cursor.visible = true;
        cruise.Disable();
        mouse.Disable();
    }
}

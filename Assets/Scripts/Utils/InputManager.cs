using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager: MonoBehaviour
{
    [SerializeField] private GameObject sceneUI;
    public InputAction esc;
    public bool escCooldown;

    void Update()
    {
        if (esc.ReadValue<float>() > 0f)
        {
            if (!escCooldown)
            {
                sceneUI.SetActive(!sceneUI.activeSelf);
                escCooldown = true;
                Invoke("SetCD", 0.2f);
            }
        }
    }

    void SetCD()
    {
        escCooldown = false;
    }

    private void OnEnable()
    {
        esc.Enable();
    }

    private void OnDisable()
    {
        esc.Disable();
    }
}

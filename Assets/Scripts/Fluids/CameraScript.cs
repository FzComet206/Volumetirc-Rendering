using UnityEngine;

public class CameraScript : MonoBehaviour
{
    private void OnPreRender()
    {
        GL.wireframe = true;
    }

    private void OnPostRender()
    {
        GL.wireframe = false;
    }
}

using UnityEngine;

public class testVolume : MonoBehaviour
{
    [SerializeField] private ComputeShader volume;
    private RenderTexture target;
    private Camera cam;

    private void Start()
    {
        cam = GetComponent<Camera>();
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        UpdateRenderTexture();
        volume.SetTexture(0, "src", src);
        volume.SetTexture(0, "target", target);
        int threadGroupX = Mathf.CeilToInt(cam.pixelWidth / 8f);
        int threadGroupY = Mathf.CeilToInt(cam.pixelHeight / 8f);
        volume.Dispatch(0, threadGroupX, threadGroupY, 1);
        Graphics.Blit(target, dest);
    }
    
    private void UpdateRenderTexture()
    {
        if (target == null || target.width != cam.pixelWidth || target.height != cam.pixelHeight)
        {
            if (target != null)
            {
                target.Release();
            }
            target = new RenderTexture(cam.pixelWidth, cam.pixelHeight, 0, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear);
            target.enableRandomWrite = true;
            target.Create();
        }
    }
}


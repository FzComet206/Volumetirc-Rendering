using UnityEngine;

public class RealtimeInput
{
    public float viscosity;
    public float diffusion;
    public float dissipation;
    public float simulationSpeed;
    public bool pause;

    public float outputForce;
    public float outputDensity;
}

public class SimAndRender: MonoBehaviour
{
    [SerializeField] private ComputeShader stableFluid;
    [SerializeField] private ComputeShader volumeRender;
    [SerializeField] Camera cam;
    
    private GameObject sceneUI;
    private RenderTexture target;

    private int gridSize = 128;
    private ComputeBuffer simulationGrid0ne; 
    private ComputeBuffer simulationGridTwo; 
    
    private void Start()
    {
        sceneUI = Resources.FindObjectsOfTypeAll<SceneUI>()[0].gameObject;
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (sceneUI.activeSelf)
        {
            Graphics.Blit(src, dest);
            return;
        }
        UpdateRenderTexture();
        volumeRender.SetTexture(0, "source", src);
        volumeRender.SetTexture(0, "target", target);

        int threadGroupX = Mathf.CeilToInt(cam.pixelWidth / 8f);
        int threadGroupY = Mathf.CeilToInt(cam.pixelHeight / 8f);
        
        volumeRender.Dispatch(0, threadGroupX, threadGroupY, 1);
        Graphics.Blit(target, dest);
    }

    private void CreateComputeBuffer()
    {
        simulationGrid0ne =
            new ComputeBuffer(gridSize * gridSize * gridSize, sizeof(float), ComputeBufferType.Structured);
        simulationGridTwo =
            new ComputeBuffer(gridSize * gridSize * gridSize, sizeof(float), ComputeBufferType.Structured);
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

    private void OnDestroy()
    {
        if (simulationGrid0ne != null)
        {
            simulationGrid0ne.Dispose();
        }

        if (simulationGridTwo != null)
        {
            simulationGridTwo.Dispose();
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;

public class SimRenderBox : MonoBehaviour
{
    private GameObject sceneUI;
    private Camera cam;

    private Material activeM;
    [SerializeField] private GameObject cube1;
    [SerializeField] private GameObject box;

    // shaders
    [SerializeField] private ComputeShader clouds;
    
    // sim and render grids
    private RenderTexture renderGrid;
    
    // things to do with rendering
    [SerializeField] private Shader volumeRender;
    [SerializeField] public Transform lightOne;
    [HideInInspector] public Material volumeMaterial;

    // rendering input
    private Vector3 lightPosition;
    [Header("VolumeRendering Input")]
    [SerializeField] Color lightColor;
    [SerializeField] Color lightColorLow;
    [SerializeField] [Range(500, 3000)] int maxRange;
    [SerializeField] float sigma_a;
    [SerializeField] float sigma_b;
    [SerializeField] float asymmetryphasefactor;
    [SerializeField] float densitytransmittancestoplimit;
    [SerializeField] private bool fixedLight;

    private int gridX = 64;
    private int gridY = 256;
    private int gridZ = 256;
    private float positionOffsetX;
    private float positionOffsetY;
    private float positionOffsetZ;
    private int tgX;
    private int tgY;
    private int tgZ;
    
    private float offset = 0;
    
    // render sphere
    private BoxCollider renderCollider;
    private ComputeBuffer cp;
    
    private void Start()
    {
        renderCollider = box.GetComponent<BoxCollider>();
        cp = new ComputeBuffer(9, sizeof(float));
        Application.targetFrameRate = 144;
        cam = GetComponent<Camera>();
        sceneUI = Resources.FindObjectsOfTypeAll<SceneUI>()[0].gameObject;
        cam.depthTextureMode = DepthTextureMode.Depth;
        
        InitTextureCloud();

        positionOffsetX = gridX / 2f;
        positionOffsetY = gridY / 2f;
        positionOffsetZ = gridZ / 2f;
        
        tgX = gridX / 4;
        tgY = gridY / 4;
        tgZ = gridZ / 4;
        
        // a box
        cube1.transform.localScale = Vector3.one * gridZ;
    }

    private void Update()
    {
        lightPosition = lightOne.position;
        lightOne.gameObject.SetActive(!fixedLight);
        UpdateBound();
    }

    private void FixedUpdate()
    {
        CloudRoutine();
    }

    private void UpdateBound()
    {
        Bounds b = renderCollider.bounds;
        
        float[] bounds = new float[9];
        
        bounds[0] = b.center.x;
        bounds[1] = b.center.y;
        bounds[2] = b.center.z;

        bounds[3] = b.min.x;
        bounds[4] = b.min.y;
        bounds[5] = b.min.z;
        
        bounds[6] = b.max.x;
        bounds[7] = b.max.y;
        bounds[8] = b.max.z;
        
        cp.SetData(bounds);
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (sceneUI.activeSelf)
        {
            Graphics.Blit(src, dest);
            return;
        }
        
        // handle rendering
        SetRenderInput();
        Graphics.Blit(src, dest, volumeMaterial);
    }

    private void CloudRoutine()
    {
        offset += Time.fixedDeltaTime * 40f;
        clouds.SetTexture(0, "Grid", renderGrid);
        clouds.SetFloat("offset", offset);
        clouds.Dispatch(0, tgX, tgY, tgZ);
    }

    private void SetRenderInput()
    {
        UpdateMaterial();
        volumeMaterial.SetBuffer("bounds", cp);
        
        volumeMaterial.SetFloat("lightX", lightPosition.x);
        volumeMaterial.SetFloat("lightY", lightPosition.y);
        volumeMaterial.SetFloat("lightZ", lightPosition.z);
        
        volumeMaterial.SetFloat("positionOffsetX", positionOffsetX);
        volumeMaterial.SetFloat("positionOffsetY", positionOffsetY);
        volumeMaterial.SetFloat("positionOffsetZ", positionOffsetZ);
        
        volumeMaterial.SetInt("gridSizeX", gridX);
        volumeMaterial.SetInt("gridSizeY", gridY);
        volumeMaterial.SetInt("gridSizeZ", gridZ);
        
        volumeMaterial.SetColor("lightColor0", lightColorLow);
        volumeMaterial.SetColor("lightColor1", lightColor);
        volumeMaterial.SetInt("maxRange", maxRange);
        
        volumeMaterial.SetFloat("sigma_a", sigma_a);
        volumeMaterial.SetFloat("sigma_b", sigma_b);
        volumeMaterial.SetFloat("asymmetryPhaseFactor", asymmetryphasefactor);
        volumeMaterial.SetFloat("densityTransmittanceStopLimit", densitytransmittancestoplimit);
        
        volumeMaterial.SetInt("fixedLight", fixedLight? 1 : 0);
        volumeMaterial.SetTexture("Grid", renderGrid);
    }
    
    private void InitTextureCloud()
    {
        renderGrid = InitTexture(RenderTextureFormat.R16, TextureWrapMode.Mirror);
    }

    private void OnDestroy()
    {
        Destroy(renderGrid);
    }

    RenderTexture InitTexture(RenderTextureFormat format, TextureWrapMode wrap = TextureWrapMode.Clamp)
    {
        RenderTexture rt;
        rt= new RenderTexture(gridX, gridY, 0);
        rt.dimension = TextureDimension.Tex3D;
        rt.filterMode = FilterMode.Trilinear;
        rt.volumeDepth = gridZ;
        rt.format = format;
        rt.wrapMode = wrap;
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    private void UpdateMaterial()
    {
        if (volumeMaterial == null || volumeMaterial.shader != volumeRender)
        {
            volumeMaterial = new Material(volumeRender);
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;

public class SimAndRender: MonoBehaviour
{
    private GameObject sceneUI;
    private Camera cam;

    private Material activeM;
    [SerializeField] private GameObject cube;
    [SerializeField] private GameObject cube1;
    [SerializeField] private GameObject fresnel;

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

    private int gridSize = 256;
    private float offset = 0;
    private float positionOffset;
    private int tg;
    
    // render sphere
    private float renderSphereRadius;
    private float renderSphereOrigin;
    
    private void Start()
    {
    
        
        
        Application.targetFrameRate = 144;
        cam = GetComponent<Camera>();
        sceneUI = Resources.FindObjectsOfTypeAll<SceneUI>()[0].gameObject;
        cam.depthTextureMode = DepthTextureMode.Depth;
        
        InitTextureCloud();

        renderSphereOrigin = 0;
        renderSphereRadius = gridSize / 2.1f;
        positionOffset = gridSize / 2f;
        tg = gridSize / 4;
        
        // a box
        cube.transform.localScale = Vector3.one * gridSize / 1.5f;
        cube1.transform.localScale = Vector3.one * gridSize * 3;
        fresnel.transform.localScale = Vector3.one * renderSphereRadius * 2;
        foreach (var componentsInChild in cube.GetComponentsInChildren<MeshRenderer>())
        {
            componentsInChild.GetComponent<MeshRenderer>().enabled = false;
        }
    }

    private void Update()
    {
        lightPosition = lightOne.position;
        lightOne.gameObject.SetActive(!fixedLight);
    }

    private void FixedUpdate()
    {
        CloudRoutine();
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
        clouds.SetInt("gridSize", gridSize);
        clouds.SetFloat("offset", offset);
        clouds.Dispatch(0, tg, tg, tg);
    }

    private void SetRenderInput()
    {
        UpdateMaterial();
        volumeMaterial.SetFloat("origin", renderSphereOrigin);
        volumeMaterial.SetFloat("radius", renderSphereRadius);
        
        volumeMaterial.SetFloat("lightX", lightPosition.x);
        volumeMaterial.SetFloat("lightY", lightPosition.y);
        volumeMaterial.SetFloat("lightZ", lightPosition.z);
        volumeMaterial.SetFloat("positionOffset", positionOffset);
        
        volumeMaterial.SetInt("gridSize", gridSize);
        
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
        renderGrid = InitTexture(gridSize, RenderTextureFormat.R16, TextureWrapMode.Mirror);
    }

    private void OnDestroy()
    {
        Destroy(renderGrid);
    }

    RenderTexture InitTexture(int size, RenderTextureFormat format, TextureWrapMode wrap = TextureWrapMode.Clamp)
    {
        RenderTexture rt;
        rt= new RenderTexture(size, size, 0);
        rt.dimension = TextureDimension.Tex3D;
        rt.filterMode = FilterMode.Trilinear;
        rt.volumeDepth = gridSize;
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
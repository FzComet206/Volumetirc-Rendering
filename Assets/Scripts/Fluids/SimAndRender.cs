using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class SimAndRender: MonoBehaviour
{
    private GameObject sceneUI;
    private Camera cam;

    private Material activeM;
    [SerializeField] private Material mCloud;
    [SerializeField] private Material mFluid;
    [SerializeField] [Range(2, 20)] private int gizmoMeshRes = 6;
    private Mesh gizmoMesh;
    private int gizmoScale = 128;

    public List<Vector3[]> glWireVertices;
    public List<int[]> glWireTriangles;
    
    // shaders
    [SerializeField] private ComputeShader clouds;
    [SerializeField] private ComputeShader stableFluids;
    
    // sim and render grids
    private RenderTexture renderGrid;
    
    private RenderTexture density0;
    private RenderTexture density1;
    private RenderTexture densityTemp;
    
    private RenderTexture p0;
    private RenderTexture p1;
    private RenderTexture div3f;
    
    private RenderTexture velocity0;
    private RenderTexture velocity1;
    private RenderTexture velocityTemp;
    
    // kernels
    private int addDensity;
    private int addVelocity;
    private int densityDiffuse;
    private int viscousDiffuse;
    private int densityAdvect;
    private int velocityAdvect;
    private int project0;
    private int project1;
    private int project2;
    private int setZero;
    
    // fluid input
    [SerializeField] float diff;
    [SerializeField] float visc;
    [SerializeField] float speed;

    // things to do with rendering
    [SerializeField] private Shader volumeRender;
    [SerializeField] public Transform lightOne;
    [HideInInspector] public Material volumeMaterial;

    // rendering input
    private Vector3 lightPosition;
    [Header("VolumeRendering Input")]
    [SerializeField] Color lightColor;
    [SerializeField] [Range(500, 3000)] int maxRange;
    [SerializeField] float sigma_a;
    [SerializeField] float sigma_b;
    [SerializeField] float asymmetryphasefactor;
    [SerializeField] float densitytransmittancestoplimit;
    [SerializeField] private bool fixedLight;
    
    private int gridSize = 256;
    private float gridToWorld;
    private float offset = 0;
    private bool fluids = false;
    private int tg;
    private int iterations = 6;
    
    // render sphere
    private float renderSphereRadius;
    private float renderSphereOrigin;
    
    private void Start()
    {
        // todo
        
        // fix the density off center prob
        // check copy texture 
        // add randomness in velocity dir
        // add vorticity
        
        Application.targetFrameRate = 144;
        cam = GetComponent<Camera>();
        sceneUI = Resources.FindObjectsOfTypeAll<SceneUI>()[0].gameObject;
        cam.depthTextureMode = DepthTextureMode.Depth;
        if (SceneManager.GetActiveScene().buildIndex == 1) fluids = true;
        
        if (!fluids)
        {
            Debug.Log("loading cloud");
            InitTextureCloud();
            activeM = mCloud;
        }
        else
        {
            Debug.Log("loading fluids");
            gridSize = 128;
            InitTextureFluid();
            addDensity = stableFluids.FindKernel("AddDensity");
            addVelocity = stableFluids.FindKernel("AddVelocity");
            densityAdvect = stableFluids.FindKernel("DensityAdvect");
            velocityAdvect = stableFluids.FindKernel("VelocityAdvect");
            densityDiffuse = stableFluids.FindKernel("DensityDiffusion");
            viscousDiffuse = stableFluids.FindKernel("ViscousDiffusion");
            project0 = stableFluids.FindKernel("Project0");
            project1 = stableFluids.FindKernel("Project1");
            project2 = stableFluids.FindKernel("Project2");
            setZero = stableFluids.FindKernel("SetZero");
            activeM = mFluid;
        }
        InitGizmosMesh();

        float origin = gizmoMeshRes * gizmoScale / 2f;
        float radius = gizmoMeshRes * gizmoScale / 3f;
        renderSphereOrigin = origin;
        renderSphereRadius = radius;
        tg = gridSize / 4;
        gridToWorld = gizmoScale * (gizmoMeshRes - 1) / (float) gridSize;
    }

    private void Update()
    {
        lightPosition = lightOne.position;
        lightOne.gameObject.SetActive(!fixedLight);
    }

    private void FixedUpdate()
    {
        if (fluids)
        {
            FluidRoutine();
        }
        else
        {
            CloudRoutine();
        }
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

    (RenderTexture one, RenderTexture two) Swap(RenderTexture one, RenderTexture two)
    {
        return (two, one);
    }
    
    private void FluidRoutine()
    {
        if (offset < 100f)
        {
            offset += Time.fixedDeltaTime * 7;
        }
        
        stableFluids.SetInt("gridWidth", gridSize);
        stableFluids.SetFloat("dt", Time.fixedDeltaTime);
        stableFluids.SetFloat("diff", diff);
        stableFluids.SetFloat("visc", visc);
        stableFluids.SetFloat("offset", offset);
        
        // set velocity 1 and density 1 to all zeros
        stableFluids.SetTexture(setZero, "setZero", density1);
        stableFluids.SetTexture(setZero, "setZero3f", velocity1);
        stableFluids.Dispatch(setZero, tg, tg, tg);
        
        for (int i = 0; i < iterations; i++)
        {
            // density diffuse
            Graphics.CopyTexture(density1, densityTemp);
            stableFluids.SetTexture(densityDiffuse,"DensityTemp", densityTemp);
            stableFluids.SetTexture(densityDiffuse,"DensityRead", density0);
            stableFluids.SetTexture(densityDiffuse,"DensityWrite", density1);
            stableFluids.Dispatch(densityDiffuse, tg, tg, tg);
            
            // velocity diffuse
            Graphics.CopyTexture(velocity1, velocityTemp);
            stableFluids.SetTexture(viscousDiffuse,"VelocityTemp", velocityTemp);
            stableFluids.SetTexture(viscousDiffuse,"VelocityRead", velocity0);
            stableFluids.SetTexture(viscousDiffuse,"VelocityWrite", velocity1);
            stableFluids.Dispatch(viscousDiffuse, tg, tg, tg);
        }
        
        // density advect
        stableFluids.SetTexture(densityAdvect, "VelocityRead", velocity1);
        stableFluids.SetTexture(densityAdvect, "DensityRead", density1);
        stableFluids.SetTexture(densityAdvect, "DensityWrite", density0);
        stableFluids.Dispatch(densityAdvect, tg, tg, tg);
        
        // velocity advect
        stableFluids.SetTexture(velocityAdvect, "VelocityRead", velocity1);
        stableFluids.SetTexture(velocityAdvect, "VelocityWrite", velocity0);
        stableFluids.Dispatch(velocityAdvect, tg, tg, tg);
        
        // density add
        stableFluids.SetTexture(addDensity, "DensityRead", density0);
        stableFluids.SetTexture(addDensity, "DensityWrite", density1);
        stableFluids.Dispatch(addDensity, tg, tg, tg);
        
        // velocity add
        stableFluids.SetTexture(addVelocity, "VelocityRead", velocity0);
        stableFluids.SetTexture(addVelocity, "VelocityWrite", velocity1);
        stableFluids.Dispatch(addVelocity, tg, tg, tg);
        
        
        // at this point, density1 / velocity1 are the newest buffer, we enforce mass conserving laws on velocity field
        
        // divergence 
        stableFluids.SetTexture(project0, "VelocityRead", velocity1);
        stableFluids.SetTexture(project0, "div", div3f);
        stableFluids.SetTexture(project0, "pWrite", p1);
        stableFluids.Dispatch(project0, tg, tg, tg);

        // jacobi 
        
        for (int i = 0; i < iterations; i++)
        {
            (p0, p1) = Swap(p0, p1);
            Graphics.CopyTexture(p0, p1);
            stableFluids.SetTexture(project1, "div", div3f);
            stableFluids.SetTexture(project1, "pRead", p0);
            stableFluids.SetTexture(project1, "pWrite", p1);
            stableFluids.Dispatch(project1, tg, tg, tg);
        }
        
        // project
        stableFluids.SetTexture(project2, "VelocityRead", velocity1);
        stableFluids.SetTexture(project2, "VelocityWrite", velocity0);
        stableFluids.SetTexture(project2, "pRead", p1);
        stableFluids.Dispatch(project2, tg, tg, tg);
        
        renderGrid = density1;
        (density0, density1) = Swap(density0, density1);
    }

    private void CloudRoutine()
    {
        offset += Time.fixedDeltaTime * 10f;
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
        
        volumeMaterial.SetInt("gridSize", gridSize);
        volumeMaterial.SetFloat("gridToWorld", gridToWorld);
        
        volumeMaterial.SetColor("lightColor", lightColor);
        volumeMaterial.SetInt("maxRange", maxRange);
        
        volumeMaterial.SetFloat("sigma_a", sigma_a);
        volumeMaterial.SetFloat("sigma_b", sigma_b);
        volumeMaterial.SetFloat("asymmetryPhaseFactor", asymmetryphasefactor);
        volumeMaterial.SetFloat("densityTransmittanceStopLimit", densitytransmittancestoplimit);
        
        volumeMaterial.SetInt("fixedLight", fixedLight? 1 : 0);
        volumeMaterial.SetTexture("Grid", renderGrid);
    }

    private void InitTextureFluid()
    {
        density0 = InitTexture(gridSize, RenderTextureFormat.R16);
        density1 = InitTexture(gridSize, RenderTextureFormat.R16);
        densityTemp = InitTexture(gridSize, RenderTextureFormat.R16);
        
        velocity0 = InitTexture(gridSize, RenderTextureFormat.ARGB32);
        velocity1 = InitTexture(gridSize, RenderTextureFormat.ARGB32);
        velocityTemp = InitTexture(gridSize, RenderTextureFormat.ARGB32);
        
        p0 = InitTexture(gridSize, RenderTextureFormat.R16);
        p1 = InitTexture(gridSize, RenderTextureFormat.R16);
        div3f = InitTexture(gridSize, RenderTextureFormat.ARGB32);
    }
    
    private void InitTextureCloud()
    {
        renderGrid = InitTexture(gridSize, RenderTextureFormat.R16, TextureWrapMode.Mirror);
    }

    private void OnDestroy()
    {
        if (fluids)
        {
            Destroy(density0);
            Destroy(density1);
            Destroy(densityTemp);
            
            Destroy(velocity0);
            Destroy(velocity1);
            Destroy(velocityTemp);
            
            Destroy(p0);
            Destroy(p1);
            Destroy(div3f);
        }
        else
        {
            Destroy(renderGrid);
        }
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
    
    private void InitGizmosMesh()
    {
        int size = gizmoMeshRes * gizmoMeshRes;
        int triSize = size * 9;
        
        Vector3[] verts = new Vector3[size];
        for (int i = 0; i < gizmoMeshRes; i++)
        {
            for (int j = 0; j < gizmoMeshRes; j++)
            {
                verts[i * gizmoMeshRes + j] = new Vector3(i, j, 0);
            }
        }

        int triIndex = 0;
        int[] tris = new int[triSize];
        for (int i = 0; i < size; i++)
        {
            int a = i;
            int b = i + 1;
            int c = i + gizmoMeshRes;
            int d = i + gizmoMeshRes + 1;

            if (i > size - gizmoMeshRes) { continue; }
            if (i % gizmoMeshRes == gizmoMeshRes - 1) { continue; }
            
            if (b < size && c < size && d < size)
            {
                tris[triIndex] = a;
                tris[triIndex + 1] = c;
                tris[triIndex + 2] = d;
                
                tris[triIndex + 3] = a;
                tris[triIndex + 4] = d;
                tris[triIndex + 5] = b;
                
                tris[triIndex + 6] = a;
                tris[triIndex + 7] = c;
                tris[triIndex + 8] = b;
                
                triIndex += 9;
            }
        }

        gizmoMesh = new Mesh();
        gizmoMesh.vertices = verts;
        gizmoMesh.triangles = tris;
        gizmoMesh.RecalculateNormals();
        gizmoMesh.RecalculateBounds();

        GameObject[] objs = new GameObject[6];
        for (int i = 0; i < 6; i++)
        {
            GameObject o = new GameObject(i.ToString(), typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            objs[i] = o;
            o.GetComponent<MeshFilter>().mesh = gizmoMesh;
            o.GetComponent<MeshRenderer>().sharedMaterial = activeM;
            o.GetComponent<MeshCollider>().sharedMesh = gizmoMesh;
            o.transform.localScale = Vector3.one * gizmoScale;
            o.layer = 6;
        }
        
        Vector3 pos = Vector3.zero;
        float offset = (gizmoMeshRes - 1) * gizmoScale;
        objs[0].transform.position = new Vector3(pos.x, pos.y, pos.z);
        
        objs[1].transform.position = new Vector3(pos.x + offset, pos.y, pos.z + offset);
        objs[1].transform.Rotate(Vector3.up, 180);
        
        objs[2].transform.position = new Vector3(pos.x, pos.y, pos.z + offset);
        objs[2].transform.Rotate(Vector3.right, -90);
        
        objs[3].transform.position = new Vector3(pos.x, pos.y + offset, pos.z);
        objs[3].transform.Rotate(Vector3.right, 90);
        
        objs[4].transform.position = new Vector3(pos.x + offset, pos.y, pos.z);
        objs[4].transform.Rotate(Vector3.up, -90);
        
        objs[5].transform.position = new Vector3(pos.x, pos.y, pos.z + offset);
        objs[5].transform.Rotate(Vector3.up, 90);

        glWireVertices = new List<Vector3[]>();
        glWireTriangles = new List<int[]>();

        for (int i = 0; i < 6; i++)
        {
            Vector3[] tempV = new Vector3[size];
            Vector3[] srcV = objs[i].GetComponent<MeshFilter>().mesh.vertices;
            Array.Copy(srcV, tempV, srcV.Length);
            
            int[] tempT = new int[triSize];
            int[] srcT = objs[i].GetComponent<MeshFilter>().mesh.triangles;
            Array.Copy(srcT, tempT, srcT.Length);

            for (int j = 0; j < tempV.Length; j++)
            {
                tempV[j] = objs[i].transform.TransformPoint(tempV[j]);
            }
            
            glWireVertices.Add(tempV);
            glWireTriangles.Add(tempT);
        }
    }
}
using System;
using System.Collections.Generic;
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

[System.Serializable]
public class VolumeRenderingInput
{
}

public class SimAndRender: MonoBehaviour
{
    private GameObject sceneUI;
    private Camera cam;

    [SerializeField] private Material m;
    [SerializeField] [Range(2, 20)] private int gizmoMeshRes = 6;
    private Mesh gizmoMesh;
    private int gizmoScale = 100;

    public List<Vector3[]> glWireVertices;
    public List<int[]> glWireTriangles;
    
    // things to do with simulation
    [SerializeField] private ComputeShader stableFluid;
    [SerializeField] private GameObject lightOne;
    private ComputeBuffer simulationGrid0ne; 
    private ComputeBuffer simulationGridTwo;
    private int gridSize = 512;
    
    // things to do with rendering
    [SerializeField] private Shader volumeRender;
    [HideInInspector] public Material volumeMaterial;

    // rendering input
    private Vector3 lightPosition;
    [Header("VolumeRendering Input")]
    [SerializeField] Color lightColor;
    [SerializeField] float maxRange;
    [SerializeField] float minRange;
    [SerializeField] int steps;
    [SerializeField] int lightStepsPer100Distance;
    [SerializeField] float sigma_a;
    [SerializeField] float sigma_b;
    [SerializeField] float opacityStopLimit;
    private void Start()
    {
        cam = GetComponent<Camera>();
        sceneUI = Resources.FindObjectsOfTypeAll<SceneUI>()[0].gameObject;
        InitGizmosMesh();
        InitBuffers();
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (sceneUI.activeSelf)
        {
            Graphics.Blit(src, dest);
            return;
        }
        
        // handle simulation
        stableFluid.SetBuffer(0, "GridOne", simulationGrid0ne);
        stableFluid.SetBuffer(0, "GridTwo", simulationGridTwo);
        stableFluid.SetInt("gridSize", gridSize);

        int threadGroupSim = gridSize / 4;
        stableFluid.Dispatch(0, threadGroupSim, threadGroupSim, threadGroupSim);

        // handle rendering
        UpdateMaterial();
        volumeMaterial.SetBuffer("densityBuffer", simulationGrid0ne);
        volumeMaterial.SetInt("gridSize", gridSize);
        
        volumeMaterial.SetColor("lightColor", lightColor);
        volumeMaterial.SetFloat("maxRange", maxRange);
        volumeMaterial.SetFloat("minRange", minRange);
        volumeMaterial.SetInt("steps", steps);
        volumeMaterial.SetInt("lightStepsPer100Distance", lightStepsPer100Distance);
        volumeMaterial.SetFloat("sigma_a", sigma_a);
        volumeMaterial.SetFloat("sigma_b", sigma_b);
        volumeMaterial.SetFloat("opacityStopLimit", opacityStopLimit);
        
        Graphics.Blit(src, dest, volumeMaterial);
    }

    private void UpdateMaterial()
    {
        if (volumeMaterial == null || volumeMaterial.shader != volumeRender)
        {
            volumeMaterial = new Material(volumeRender);
        }
    }

    private void InitBuffers()
    {
        simulationGrid0ne = new ComputeBuffer(gridSize * gridSize * gridSize, sizeof(float));
        simulationGridTwo = new ComputeBuffer(gridSize * gridSize * gridSize, sizeof(float));
    }

    private void OnDestroy()
    {
        simulationGrid0ne.Dispose();
        simulationGridTwo.Dispose();
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
            o.GetComponent<MeshRenderer>().sharedMaterial = m;
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
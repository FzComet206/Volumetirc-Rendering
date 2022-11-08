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

    [SerializeField] private Material m;
    [SerializeField] private Material glwiremat;
    
    [SerializeField] 
    [Range(2, 20)]
    private int gizmoMeshRes = 6;
    private Mesh gizmoMesh;
    private int gizmoScale = 100;

    public List<Vector3[]> glWireVertices;
    public List<int[]> glWireTriangles;

    private void Start()
    {
        sceneUI = Resources.FindObjectsOfTypeAll<SceneUI>()[0].gameObject;
        CreateComputeBuffer();
        InitGizmosMesh();
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
        }
        
        Vector3 pos = transform.position;
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

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (sceneUI.activeSelf)
        {
            Graphics.Blit(src, dest);
            return;
        }
        
        // handle rendering
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

    private void OnPreRender()
    {
        GL.wireframe = true;
    }

    private void OnPostRender()
    {
        GL.wireframe = false;
    }

    void DrawGlWire()
    {
        GL.PushMatrix();
        
        glwiremat.SetPass( 0 );
        GL.Begin(GL.LINES);

        RenderWireList(glWireVertices[0], glWireTriangles[0], new Color(Color.cyan.r, Color.cyan.g, Color.cyan.b));
        RenderWireList(glWireVertices[1], glWireTriangles[1], new Color(Color.cyan.r, Color.cyan.g, Color.cyan.b));
            
        RenderWireList(glWireVertices[2], glWireTriangles[2], new Color(Color.yellow.r, Color.yellow.g, Color.yellow.b));
        RenderWireList(glWireVertices[3], glWireTriangles[3], new Color(Color.yellow.r, Color.yellow.g, Color.yellow.b));
       
        RenderWireList(glWireVertices[4], glWireTriangles[4], new Color(Color.green.r, Color.green.g, Color.green.b));
        RenderWireList(glWireVertices[5], glWireTriangles[5], new Color(Color.green.r, Color.green.g, Color.green.b));
        
        GL.End();
        GL.PopMatrix();
    }

    void RenderWireList(Vector3[] vertices, int[] triangles, Color color)
    {
        GL.Color(color);
        for (int i = 0; i < triangles.Length; i+=6)
        {
            GL.Vertex(vertices[triangles[i]]);
            GL.Vertex(vertices[triangles[i+1]]);
            
            GL.Vertex(vertices[triangles[i+1]]);
            GL.Vertex(vertices[triangles[i+2]]);
            
            GL.Vertex(vertices[triangles[i+2]]);
            GL.Vertex(vertices[triangles[i]]);
            
            GL.Vertex(vertices[triangles[i+3]]);
            GL.Vertex(vertices[triangles[i+4]]);
            
            GL.Vertex(vertices[triangles[i+4]]);
            GL.Vertex(vertices[triangles[i+5]]);
            
            GL.Vertex(vertices[triangles[i+5]]);
            GL.Vertex(vertices[triangles[i]]);
            
            GL.Vertex(vertices[triangles[i+1]]);
            GL.Vertex(vertices[triangles[i+5]]);
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
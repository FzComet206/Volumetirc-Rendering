using UnityEngine;

public class CameraScript : MonoBehaviour
{
    [SerializeField] private Material wireMat;
    private SimAndRender simAndRender;

    private void Start()
    {
        simAndRender = FindObjectOfType<SimAndRender>();
    }

    private void OnPostRender()
    {
        DrawGlWire();
    }
    
    void DrawGlWire()
    {
        GL.PushMatrix();
        
        wireMat.SetPass( 0 );
        GL.Begin( GL.LINES );

        RenderWireList(simAndRender.glWireVertices[0], simAndRender.glWireTriangles[0], new Color(Color.cyan.r, Color.cyan.g, Color.cyan.b));
        RenderWireList(simAndRender.glWireVertices[1], simAndRender.glWireTriangles[1], new Color(Color.cyan.r, Color.cyan.g, Color.cyan.b));
            
        RenderWireList(simAndRender.glWireVertices[2], simAndRender.glWireTriangles[2], new Color(Color.yellow.r, Color.yellow.g, Color.yellow.b));
        RenderWireList(simAndRender.glWireVertices[3], simAndRender.glWireTriangles[3], new Color(Color.yellow.r, Color.yellow.g, Color.yellow.b));
        
        RenderWireList(simAndRender.glWireVertices[4], simAndRender.glWireTriangles[4], new Color(Color.green.r, Color.green.g, Color.green.b));
        RenderWireList(simAndRender.glWireVertices[5], simAndRender.glWireTriangles[5], new Color(Color.green.r, Color.green.g, Color.green.b));
        
        GL.End();
        GL.PopMatrix();
    }

    void RenderWireList(Vector3[] vertices, int[] triangles, Color color)
    {
        GL.Color(color);
        for (int i = 0; i < triangles.Length; i+=9)
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
            
            GL.Vertex(vertices[triangles[i+6]]);
            GL.Vertex(vertices[triangles[i+7]]);
            
            GL.Vertex(vertices[triangles[i+7]]);
            GL.Vertex(vertices[triangles[i+8]]);
            
            GL.Vertex(vertices[triangles[i+8]]);
            GL.Vertex(vertices[triangles[i]]);
        }
    }
}

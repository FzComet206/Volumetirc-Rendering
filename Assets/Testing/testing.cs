using UnityEngine;

public class testing : MonoBehaviour
{
    [SerializeField] private GameObject sphere;
    [SerializeField] private GameObject rayOrigin;
    
    [SerializeField] private GameObject _p0;
    [SerializeField] private GameObject _p1;
    
    // get from scene
    private Vector3 sO;
    private Vector3 rayO;
    private Vector3 rayDir = Vector3.one.normalized;
    private float rayLength = 1000;
    private Vector3 v;
    
    // to be calculated
    private Vector3 p0;
    private Vector3 p1;

    private float radius = 100;
    void Start()
    {
        sphere.transform.localScale = Vector3.one * radius * 2;
    }
    
    private void FixedUpdate()
    {
        
        sO = sphere.transform.position;
        rayO = rayOrigin.transform.position;

        v = rayO + rayDir * rayLength;
        
        _p0.transform.position = p0;
        _p1.transform.position = p1;
        
        HandleRay();
    }

    void HandleRay()
    {
        Debug.DrawRay(rayO, v, Color.green);
        SetP0();
    }

    private void SetP0()
    {
        Vector3 u = sO - rayO;
        Debug.DrawRay(rayO, u, Color.red);
        
        float x = Vector3.Dot(v.normalized, u);
        Debug.DrawRay(rayO, v.normalized * x, Color.yellow);

        Vector3 B = rayO + (v.normalized * x) - sO;
        Debug.DrawRay(sO, B, Color.magenta);

        if (B.magnitude >= radius) return;

        float a = Mathf.Sqrt(radius * radius - B.magnitude * B.magnitude);
        float p0l = x - a;
        float p1l = x + a;
        Debug.DrawRay(rayO, v.normalized * p0l, Color.white);
        
        _p0.transform.position = rayO + v.normalized * p0l;
        _p1.transform.position = rayO + v.normalized * p1l;
        
        Debug.DrawRay(sO, rayO + v.normalized * p0l - sO, Color.cyan);
        Debug.DrawRay(sO, rayO + v.normalized * p1l - sO, Color.cyan);
    }
}

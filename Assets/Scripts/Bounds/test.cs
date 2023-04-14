using UnityEngine;

public class test : MonoBehaviour
{
    [SerializeField] private GameObject box;
    private BoxCollider c;
    void Start()
    {
        c = box.GetComponent<BoxCollider>();
    }

    void Update()
    {
        Intersect();
    }

    void Intersect()
    {
        Bounds b = c.bounds;
        Vector3 origin = Vector3.one * 10;
        Vector3 dir = Vector3.forward.normalized;
        Vector3 boxMin = b.min;
        Vector3 boxMax = b.max;
        
        Debug.DrawRay(origin, dir * 100, Color.green);
        
        // calculate intersect points
        Vector3 r0 = Div(boxMin - origin, dir);
        Vector3 r1 = Div(boxMax - origin, dir);
        Vector3 rmin = Vector3.Min(r0, r1);
        Vector3 rmax = Vector3.Max(r0, r1);

        float distA = Mathf.Max(
            Mathf.Max(rmin.x, rmin.y),
            rmin.z
        );
        
        float distB = Mathf.Min(
            rmax.x,
            Mathf.Min(rmax.y, rmax.z)
        );

        float dist0 = Mathf.Max(0, distA);

        Vector3 entrance = origin + dist0 * dir;
        Vector3 exit = origin + distB * dir;
        Debug.DrawRay(entrance, Vector3.up * 10, Color.red);
        Debug.DrawRay(exit, Vector3.up * 10, Color.blue);
        // dist0 is entrance point
        // distB is exit point
    }

    Vector3 Div(Vector3 one, Vector3 two)
    {
        return new Vector3(
            one.x / two.x,
            one.y / two.y,
            one.z / two.z
        );
    }
}

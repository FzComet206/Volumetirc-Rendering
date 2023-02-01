using UnityEngine;
using Random = UnityEngine.Random;

public class LightMove : MonoBehaviour
{
    [SerializeField] private float speed;
    [SerializeField] private float radius;
    
    private Rigidbody _rigidbody;
    private SphereCollider _collider;
    private Vector3 velocity;
    
    void Start()
    {
        _collider = GetComponent<SphereCollider>();
        _rigidbody = GetComponent<Rigidbody>();

        _collider.radius = radius;
        velocity = new Vector3(Random.Range(-10, 10), Random.Range(-10, 10), Random.Range(-10, 10)).normalized * speed *
                   Time.deltaTime;
    }

    private void FixedUpdate()
    {
        _rigidbody.velocity = velocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 norm = collision.contacts[0].normal;
        velocity = Vector3.Reflect(velocity, -norm).normalized * speed * Time.fixedDeltaTime;
    }
}

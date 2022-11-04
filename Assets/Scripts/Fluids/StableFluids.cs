using UnityEngine;

[System.Serializable]
public class FluidsInput
{
    public float viscosity;
    public float diffusion;
    public float dissipation;
    public float simulationSpeed;
    public bool pause;
}
public class StableFluids : MonoBehaviour
{
    [SerializeField] private FluidsInput input;
    [SerializeField] private ComputeShader stabefluid;
    
    // handle input
    // set parameters
    // dispatch fluid simulation
}

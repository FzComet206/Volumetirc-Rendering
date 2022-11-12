Shader "Custom/VolumeRenderer"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull off zwrite off ZTest Always
        pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewVector: TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            
            // basic settings
            StructuredBuffer<float> densityBufferOne;
            int gridSize;
            float worldToGrid;
            
            // light settings
            float3 lightPosition;
            float4 lightColor;
            float maxRange;
            float minRange;
            int steps;
            int lightStepsPer100Distance;
            float sigma_a;
            float sigma_b;
            float asymmetryPhaseFactor;
            float densityThreshold;
            float densityTransmittanceStopLimit;
            float lightTransmittanceStopLimit;
                    
            int CoordToIndex(int x, int y, int z)
            {
                return 128 * 128 * x + 128 * y + z;
            }

            // Henyey-Greenstein
            float hg(float a, float g) {
                float g2 = g*g;
                return (1-g2) / (4*3.1415*pow(1+g2-2*g*(a), 1.5));
            }

            float phase(float a)
            {
                return hg(a, asymmetryPhaseFactor);
            }

            float beer(float d) {
                float beer = exp(-d);
                return beer;
            }
            
            float SampleGrid(float3 pos)
            {
                return 0;
            }

            float SampleGridTrilinear(float3 pos)
            {
                return 0;
            }
            
            float RayMarchLight(float3 pos)
            {
                float l = length(lightPosition - pos);
                int steps = (int) round(lightStepsPer100Distance * l / 100);
                float stepSize = l / steps;
                return 0;
            }
            
            v2f vert(appdata v)
            {
                v2f output;
                output.pos = UnityObjectToClipPos(v.vertex);
                output.uv = v.uv;
                
                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                output.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                return output;
            }

            float4 frag(v2f id) : SV_Target
            {
                // get ray
                float3 rayPos;
                float3 rayDir = normalize(id.viewVector);

                // sample depth
                float nonLinearDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, id.uv);
                float l = length(id.viewVector);
                // convert to linear depth and scale by view length
                float depth = LinearEyeDepth(nonLinearDepth) * l;
                // stop ray at depth
                maxRange = min(depth, maxRange); 
                
                float stepSize = (maxRange - minRange) / steps;
                float distanceTraveled = minRange;
                
                float transmittence = 1;
                float lighting = 0;

                while (distanceTraveled < maxRange)
                {
                    rayPos = rayDir * distanceTraveled;
                    float density = SampleGrid(rayPos);

                    if (density > densityThreshold)
                    {
                        // phase value depends of rayPos because of point light
                        float phaseValue = phase(dot(rayDir, lightPosition - rayPos));
                        // marching light
                        float lightTransmittance = RayMarchLight(rayPos);
                        // merge
                        lighting +=
                            density * stepSize * transmittence * lightTransmittance * phaseValue;
                        // update transmittance
                        transmittence *= beer(density * stepSize * sigma_a);

                        // exit early when opaque
                        if (transmittence < densityTransmittanceStopLimit)
                        {
                            break;
                        }
                    }
                    distanceTraveled += stepSize;
                }

                float3 background = tex2D(_MainTex, id.uv);
                float3 cloudColor = lighting * lightColor;
                float3 color = background * transmittence + cloudColor;
                return float4(color, 0);
            }

            ENDCG
        }
    }
}

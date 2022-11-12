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

            v2f vert(appdata v)
            {
                v2f output;
                output.pos = UnityObjectToClipPos(v.vertex);
                output.uv = v.uv;
                
                float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                output.viewVector = mul(unity_CameraToWorld, float4(viewVector,0));
                return output;
            }
            
            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;
            
            // basic settings
            StructuredBuffer<float> densityBufferOne;
            int gridSize;
            float gridToWorld;
            
            // light settings
            float3 lightPosition;
            float4 lightColor;
            int maxRange;
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
                return gridSize * gridSize * x + gridSize * y + z;
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
                pos = pos / gridToWorld;
                int x = round(pos.x );
                int y = round(pos.y );
                int z = round(pos.z );

                if (x < 0 || x >= gridSize || y < 0 || y >= gridSize || z < 0 || z >= gridSize)
                {
                    return 0;
                }

                float d = densityBufferOne[CoordToIndex(x, y, z)];
                if (d > densityThreshold)
                {
                    return d;
                }
                return 0;
            }

            float SampleGridTrilinear(float3 pos)
            {
                return 0;
            }
            
            float RayMarchLight(float3 pos)
            {
                float3 dirToLight = lightPosition - pos;
                float l = length(dirToLight);
                
                int steps = (int) round(lightStepsPer100Distance * l / 100);
                float stepSize = l / steps;

                for (int s = 0; s < steps; s++)
                {
                    pos += dirToLight * stepSize ;
                }
                
                
                return 0.5;
            }
            

            float4 frag(v2f id) : SV_Target
            {
                // get ray
                float3 entry = _WorldSpaceCameraPos;
                float viewLength = length(id.viewVector);
                float3 rayDir = id.viewVector / viewLength;

                // sample depth
                float nonLinearDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, id.uv);
                float l = length(id.viewVector);
                // convert to linear depth and scale by view length
                float depth = LinearEyeDepth(nonLinearDepth) * l;
                
                // stop ray at depth
                
                float range = min(depth, maxRange);
                float stepSize = maxRange / (float) steps;
                
                float distanceTraveled = 0;
                float transmittence = 1;
                float lighting = 0;
                
                while (distanceTraveled < range)
                {
                    float3 rayPos = entry + rayDir * distanceTraveled;
                    float density = SampleGrid(rayPos);

                    if (density > densityThreshold)
                    {
                        // phase value depends of rayPos because of point light
                        float phaseValue = phase(dot(rayDir, lightPosition - rayPos));
                        // marching light
                        float lightTransmittance = RayMarchLight(rayPos);
                        // merge
                        lighting +=
                            // density * stepSize * transmittence * lightTransmittance * phaseValue;
                            density * stepSize * transmittence;
                        // update transmittance
                        transmittence *= exp(-density * stepSize * sigma_a);

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

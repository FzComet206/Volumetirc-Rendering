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
            Texture3D<float4> Grid;
            SamplerState samplerGrid;
            
            int gridSize;
            float positionOffset;
            
            // light settings
            bool forward;
            
            float lightX;
            float lightY;
            float lightZ;
            
            float4 lightColor;
            int maxRange;
            float sigma_a;
            float sigma_b;
            float asymmetryPhaseFactor;
            float densityTransmittanceStopLimit;
            int fixedLight;

            // render sphere
            float origin;
            float radius;;
                    
            float remap(float v, float minOld, float maxOld, float minNew, float maxNew) {
                return minNew + (v-minOld) * (maxNew - minNew) / (maxOld-minOld);
            }
            
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
            
            float LightMarch(float3 pos)
            {
                float3 origin3 = float3(origin, origin, origin);
                // float3 dirToLight = normalize(_WorldSpaceLightPos0);
                float3 lightPos;
                if (fixedLight)
                {
                    lightPos = _WorldSpaceLightPos0;
                }
                else
                {
                    lightPos = float3(lightX, lightY, lightZ);
                }
                
                float3 lightVector = lightPos - pos;
                float3 dirToLight = normalize(lightVector);
                float lenToLight = length(lightVector);

                float stepSize = 0.1;
                float traveled = 0;
                float totalDensity = 0;
                float range;

                if (!fixedLight)
                {
                    range = lenToLight;
                }
                else
                {
                    range = 1000;
                }

                while (traveled < range)
                {
                    float3 pt = pos + dirToLight * traveled;
                    float3 uvw = pt / gridSize;
                    
                    totalDensity += Grid.SampleLevel(samplerGrid, uvw, 0).x;
                    traveled += stepSize;
                    
                    // want higher light resolution closer to light
                    stepSize += 0.1;
                    if (length(pt - origin3) > radius){break;}
                }
                return 0.005 + exp(-totalDensity * sigma_b) * (1 - 0.005);
            }
            
            float4 frag(v2f id) : SV_Target
            {
                // get ray
                float3 origin3 = float3(origin, origin, origin);
                float3 entry = _WorldSpaceCameraPos;
                float viewLength = length(id.viewVector);
                float3 rayDir = id.viewVector / viewLength;

                // sample depth
                float nonLinearDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, id.uv);
                float l = length(id.viewVector);
                // convert to linear depth and scale by view length
                float depth = LinearEyeDepth(nonLinearDepth) * l;
                // float phaseValue = phase(dot(rayDir, _WorldSpaceLightPos0));
                float phaseValue;
                
                // stop ray at depth
                float range = min(depth, maxRange);

                float stepSize = 4;
                
                float distanceTraveled = 0;
                float transmittence = 1;
                float lighting = 0;

                float3 rayPos;
                float scale = gridSize;

                float3 lightPos;
                lightPos = _WorldSpaceLightPos0;
                phaseValue = phase(dot(rayDir, lightPos));


                // rendersphere
                
                while (distanceTraveled < range)
                {
                    rayPos = entry + rayDir * distanceTraveled;

                    if (length(rayPos - origin3) > radius)
                    {
                        distanceTraveled += stepSize;
                        continue;
                    }

                    float3 samplePos = (rayPos + float3(1,1,1) * positionOffset) / scale;

                    float density = 0;
                    density = Grid.SampleLevel(samplerGrid, samplePos, 0).x;
                    
                    if (density > 0)
                    {
                        // phase value depends of rayPos because of point light
                        // marching light
                        float lightTransmittance = LightMarch(rayPos);

                        if (!fixedLight)
                        {
                            lightPos = float3(lightX, lightY, lightZ);
                            phaseValue = phase(dot(rayDir, normalize(lightPos - rayPos)));
                        }
                        
                        lighting +=
                            density * stepSize * transmittence * lightTransmittance * phaseValue;
                            // density * stepSize * transmittence;
                        // update transmittance
                        transmittence *= exp(-density * stepSize * sigma_a);


                        // exit early when opaque
                        if (transmittence < densityTransmittanceStopLimit)
                        {
                            transmittence = 0;
                            break;
                        }
                    }
                    distanceTraveled += stepSize;
                }

                float3 background = tex2D(_MainTex, id.uv);
                float3 cloudColor = lighting * lightColor * 6;
                float3 color = background * transmittence + cloudColor;

                return float4(color, 0);
            }

            ENDCG
        }
    }
}

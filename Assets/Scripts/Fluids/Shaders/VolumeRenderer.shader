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
            float gridToWorld;
            
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

            float SampleGridTrilinear(float3 pos)
            {
                float gx, gy, gz, tx, ty, tz;
                int gxi, gyi, gzi;
                float c000, c100, c010, c110, c001, c101, c011, c111;
                
                gx = pos.x / gridToWorld;
                gy = pos.y / gridToWorld;
                gz = pos.z / gridToWorld;
                
                gxi = round(gx);
                gyi = round(gy);
                gzi = round(gz);

                if (gxi < 1 || gxi >= gridSize-1 || gyi < 1 || gyi >= gridSize-1 || gzi < 1 || gzi >= gridSize-1)
                {
                    return 0;
                }
                
                tx = gx - gxi;
                ty = gy - gyi;
                tz = gz - gzi;

                c000 = densityBufferOne[CoordToIndex(gxi, gyi, gzi)];
                c100 = densityBufferOne[CoordToIndex(gxi + 1, gyi, gzi)];
                c010 = densityBufferOne[CoordToIndex(gxi, gyi + 1, gzi)];
                c110 = densityBufferOne[CoordToIndex(gxi + 1, gyi + 1, gzi)];
                c001 = densityBufferOne[CoordToIndex(gxi, gyi, gzi + 1)];
                c101 = densityBufferOne[CoordToIndex(gxi + 1, gyi, gzi + 1)];
                c011 = densityBufferOne[CoordToIndex(gxi, gyi + 1, gzi + 1)];
                c111 = densityBufferOne[CoordToIndex(gxi + 1, gyi + 1, gzi + 1)];

                return
                    (1 - tx) * (1 - ty) * (1 - tz) * c000 +
                        tx * (1 - ty) * (1 - tz) * c100 +
                            (1 - tx) * ty * (1 - tz) * c010 +
                                tx * ty * (1 - tz) * c110 +
                                    (1 - tx) * (1 - ty) * tz * c001 +
                                        tx * (1 - ty) * tz * c101 +
                                            (1 - tx) * ty * tz * c011 +
                                                tx * ty * tz * c111;
            }
            
            float LightMarch(float3 pos)
            {
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
                } else
                {
                    range = 500;
                }

                while (traveled < range)
                {
                    // marching from light to pos
                    float3 pt = pos + dirToLight * traveled;
                    float3 uvw = pt / gridSize / gridToWorld;
                    
                    totalDensity += Grid.SampleLevel(samplerGrid, uvw, 0).x;
                    traveled += stepSize;
                    
                    // want higher light resolution closer to light
                    stepSize += 0.15;
                }
                return exp(-totalDensity * sigma_b);
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
                // float phaseValue = phase(dot(rayDir, _WorldSpaceLightPos0));
                float phaseValue;
                
                // stop ray at depth
                float range = min(depth, maxRange);

                float stepSize = 5;
                
                float distanceTraveled = 0;
                float transmittence = 1;
                float lighting = 0;

                float3 rayPos;
                float scale = gridSize * gridToWorld;

                float3 lightPos;
                lightPos = _WorldSpaceLightPos0;
                phaseValue = phase(dot(rayDir, lightPos));
                
                while (distanceTraveled < range)
                {
                    rayPos = entry + rayDir * distanceTraveled;

                    float3 samplePos = rayPos / scale;

                    float density;
                    if (samplePos.x < 0 || samplePos.x > 1 || samplePos.y < 0 || samplePos.y > 1 || samplePos.z < 0 || samplePos.z > 1)
                    {
                        density = 0;
                    }
                    else
                    {
                        density = Grid.SampleLevel(samplerGrid, samplePos, 0).x;
                    }
                    
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

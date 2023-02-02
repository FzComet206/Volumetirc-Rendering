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
            
            float4 lightColor0;
            float4 lightColor1;
            int maxRange;
            float sigma_a;
            float sigma_b;
            float asymmetryPhaseFactor;
            float densityTransmittanceStopLimit;
            int fixedLight;

            // render sphere
            float origin;
            float radius;;

            struct PointSet
            {
                float3 p0;
                float3 p1;
            };
                    
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

            PointSet GetPoints(float3 pos, float3 dir)
            {
                PointSet points;

                // origin of sphere
                float3 origin3 = float3(origin, origin, origin);
                float3 u = origin3 - pos;
                
                float3 normalDir = normalize(dir);
                float x = dot(normalDir, u);

                // prevent mirroring
                if (x < 0 && length(pos - origin3) > radius)
                {
                    points.p0 = float3(0,0,0);
                    points.p1 = float3(0,0,0);
                    return points;
                }
                
                float3 B = (pos + normalDir * x) - origin3;

                float lB = length(B);

                // skip if not intersect
                if (lB >= radius)
                {
                    points.p0 = float3(0,0,0);
                    points.p1 = float3(0,0,0);
                    return points;
                }

                float a = sqrt(radius * radius - lB * lB);

                points.p0 = pos + dir * (x - a + 2);
                points.p1 = pos + dir * (x + a - 2);
                
                if (length(pos - origin3) < radius)
                {
                    points.p0 = pos;
                }

                return points;
            }
            
            float LightMarch(float3 pos)
            {
                float3 origin3 = float3(origin, origin, origin);
                // float3 dirToLight = normalize(_WorldSpaceLightPos0);
                float3 lightPos;
                lightPos = float3(lightX, lightY, lightZ);
                
                float3 lightVector = lightPos - pos;
                float3 dirToLight = normalize(lightVector);

                float3 exit = GetPoints(pos, dirToLight).p1;
                
                float totalDensity = 0;
                float range;

                range = length(exit - pos) - 0.1;
                
                int stepsPer100Range = 30;
                int steps = ceil(range / 100.0 * (float) stepsPer100Range);
                float stepSize = range / (float) steps;


                
                // light march
                float traveled = 0;
                for (int i = 0; i < steps; i++)
                {
                    float3 pt = pos + dirToLight * traveled;
                    float3 uvw = pt / gridSize;
                    totalDensity += Grid.SampleLevel(samplerGrid, uvw, 0).x;
                    traveled += stepSize;
                }
                
                return 0.005 + exp(-totalDensity * sigma_b) * (1 - 0.005);
            }

            

            
            float4 frag(v2f id) : SV_Target
            {
                float3 background = tex2D(_MainTex, id.uv);
                // get ray
                float3 cam = _WorldSpaceCameraPos;
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
                
                float transmittence = 1;
                float lighting = 0;

                float3 rayPos;
                float scale = gridSize;

                float3 lightPos;

                // get points p0 and p1
                float3 entry = cam;
                PointSet points = GetPoints(entry, rayDir);
                entry = points.p0;

                float travelDist = length(points.p1 - points.p0);
                
                int stepsPer100Range = 50;
                int steps = ceil(travelDist / 100.0 * (float) stepsPer100Range);
                
                float stepSize = travelDist / (float) steps;
                
                if (travelDist < 0.1) {return float4(background, 0);};

                // rendersphere

                // start ray marching
                for (int i = 0 ; i < steps; i++)
                {
                    if (length(entry - cam) > range) {break;}
                    
                    rayPos = entry;

                    float3 samplePos = (rayPos + float3(1,1,1) * positionOffset) / scale;

                    float density = Grid.SampleLevel(samplerGrid, samplePos, 0).x;
                    
                    if (density > 0)
                    {
                        // phase value depends of rayPos because of point light
                        // marching light
                        float lightTransmittance = LightMarch(rayPos);

                        lightPos = float3(lightX, lightY, lightZ);
                        phaseValue = phase(dot(rayDir, normalize(lightPos - rayPos)));
                        
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

                    entry = entry + rayDir * stepSize;
                }

                float3 lightColor;
                lightColor.x = remap(transmittence, 0, 0.5, lightColor1.x, lightColor0.x);
                lightColor.y = remap(transmittence, 0, 0.5, lightColor1.y, lightColor0.y);
                lightColor.z = remap(transmittence, 0, 0.5, lightColor1.z, lightColor0.z);
                
                float3 cloudColor = lighting * lightColor * 3;
                float3 color = background * transmittence + cloudColor;

                return float4(color, 0);
            }

            ENDCG
        }
    }
}

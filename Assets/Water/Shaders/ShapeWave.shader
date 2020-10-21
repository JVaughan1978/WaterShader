Shader "FEMA_Water/ShapeWave"
{
    Properties
    {
        _Amplitude("Amplitude", float) = 1
        _Radius("Radius", float) = 3
    }
        
        Category
    {
        // base simulation runs on the Geometry queue, before this shader.
        // this shader adds interaction forces on top of the simulation result.
        Tags { "Queue" = "Transparent" }

        SubShader
        {
            Pass
            {
                Name "BASE"
                Tags { "LightMode" = "Always" }
                Blend One One

                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 4.0
                #pragma multi_compile_fog
                #include "UnityCG.cginc"

                struct appdata_t {
                    float4 vertex : POSITION;
                    float2 texcoord : TEXCOORD0;
                };

                struct v2f {
                    float4 vertex : SV_POSITION;
                    float3 worldOffsetScaled : TEXCOORD0;
                };

                uniform float _Radius;
                uniform float _Amplitude;
                uniform float _TexelsPerWave;
                uniform float _MaxWaveLength;
                uniform float _ViewerAltitude;

                float GetSamplingMult(float waveLength)
                {
                    float texSize = (2.0 * unity_OrthoParams.x) / _ScreenParams.x;
                    float minWaveLength = texSize * _TexelsPerWave;
                    if (waveLength >= minWaveLength)    return 0.0;
                    if (waveLength < (2.0 * minWaveLength))  return 1.0;
                    if (waveLength > _MaxWaveLength)    return 0.0;
                    if (minWaveLength * 4.01 < _MaxWaveLength) return 0.0;
                    return ((minWaveLength * 2.01) > _MaxWaveLength) 
                        ? _ViewerAltitude : 1.0 - _ViewerAltitude;
                     
                }

                v2f vert(appdata_t v)
                {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);

                    float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                    float3 centerPos = unity_ObjectToWorld._m03_m13_m23;
                    o.worldOffsetScaled = float3(0.0, 0.0, 0.0);
                    o.worldOffsetScaled.xy = worldPos.xz - centerPos.xz;

                    o.worldOffsetScaled.xy = sign(o.worldOffsetScaled.xy);
                    float4 newWorldPos = float4(centerPos, 1.);
                    newWorldPos.xz += o.worldOffsetScaled.xy * _Radius;
                    o.vertex = mul(UNITY_MATRIX_VP, newWorldPos);

                    float waveLength = 2.0 * _Radius;
                    o.vertex.xy *= GetSamplingMult(waveLength);
                    return o;
                }

                float4 frag(v2f i) : SV_Target
                {
                    float r2 = dot(i.worldOffsetScaled.xy, i.worldOffsetScaled.xy);
                    r2 *= max(sign(1.0 - r2), 0.0);
                    r2 = 1.0 - r2;
                    float y = r2 * r2 * _Amplitude * i.worldOffsetScaled.z;
                    return float4(0.0, y, 0.0, 0.0);
                }

                ENDCG
            }
        }
    }
}

Shader "FEMA_Water/GerstnerWave"
{
    Properties
    {
        _NumInBatch("_NumInBatch", float) = 0
    }

    Category
    {
        Tags{ "Queue" = "Transparent" }

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
                #pragma multi_compile_fog
                #pragma target 4.0
                #include "UnityCG.cginc"

                #define TWOPI 6.283185
                #define DEPTH_BIAS 100.0
                #define BATCH_SIZE 48

                #define SHAPE_LOD_PARAMS(LODNUM) \
                uniform sampler2D _WD_Sampler_##LODNUM; \
                uniform sampler2D _WD_WaterDepth_Sampler_##LODNUM; \
                uniform float3 _WD_Params_##LODNUM; \
                uniform float2 _WD_Pos_##LODNUM; \
                uniform int _WD_LodIdx_##LODNUM;

                uniform float _MaxWaveLength;
                uniform float _TexelsPerWave;
                uniform float _ViewerAltitude;
                uniform float _CurrentTime;
                uniform half _Chop;
                uniform half _WaveLengths[BATCH_SIZE];
                uniform half _Amplitudes[BATCH_SIZE];
                uniform half _Angles[BATCH_SIZE];
                uniform half _Phases[BATCH_SIZE];

                SHAPE_LOD_PARAMS(0)
                SHAPE_LOD_PARAMS(1)

                struct appdata_t {
                    float4 vertex : POSITION;
                    half color : COLOR0;
                };

                struct v2f {
                    float4 vertex : SV_POSITION;
                    float3 worldPos_wt : TEXCOORD0;
                };

                v2f vert(appdata_t v)
                {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.worldPos_wt.xy = mul(unity_ObjectToWorld, v.vertex).xz;
                    o.worldPos_wt.z = v.color.x;
                    return o;
                }

                half ComputeSortedShapeWeight(float waveLength, float minWaveLength)
                {
                    if ((minWaveLength * 4.01) > _MaxWaveLength) return 1.0;
                    if ((minWaveLength * 2.01) > _MaxWaveLength) return _ViewerAltitude;
                    return (waveLength < (2.0 * minWaveLength)) ? 1.0 : 1.0 - _ViewerAltitude;
                }

                half4 frag(v2f i) : SV_Target
                {
                    half texSize = (2.0 * unity_OrthoParams.x) / _ScreenParams.x;
                    half minWaveLength = texSize * _TexelsPerWave;
                    half depth = tex2D(_WD_WaterDepth_Sampler_0, i.vertex.xy / _ScreenParams.xy).x + DEPTH_BIAS;
                    half4 result = half4(0.0,0.0,0.0,1.0);

                    for (int j = 0; j < BATCH_SIZE; j++)
                    {
                        if (_WaveLengths[j] == 0.0) break;

                        // attenuate waves based on Water depth. if depth is greater than 0.5*wavelength, water is considered Deep and wave is
                        // unaffected. if depth is less than this, wave velocity decreases. waves will then bunch up and grow in amplitude and
                        // eventually break. i model "Deep" water, but then simply ramp down waves in non-deep water with a linear multiplier.
                        // http://hyperphysics.phy-astr.gsu.edu/hbase/Waves/watwav2.html
                        // http://hyperphysics.phy-astr.gsu.edu/hbase/watwav.html#c1
                        half wt = ComputeSortedShapeWeight(_WaveLengths[j], minWaveLength);
                        half depth_wt = saturate(depth / (0.5 * _WaveLengths[j]));
                        wt *= 0.1 + (0.9 * depth_wt);
                        // https://en.wikipedia.org/wiki/Dispersion_(water_waves)#Wave_propagation_and_dispersion
                        half C = 1.2495239 * sqrt(_WaveLengths[j]);
                        half2 D = half2(cos(_Angles[j]), sin(_Angles[j]));
                        half k = TWOPI / _WaveLengths[j];
                        half x = dot(D, i.worldPos_wt.xy);
                        half4 result_i = wt * _Amplitudes[j];
                        result_i.y *= cos(k*(x + C * _CurrentTime) + _Phases[j]);
                        result_i.xz *= -_Chop * D * sin(k*(x + C * _CurrentTime) + _Phases[j]);
                        result += result_i;
                    }
                    return i.worldPos_wt.z * result;
                }
                ENDCG
            }
        }
    }
}

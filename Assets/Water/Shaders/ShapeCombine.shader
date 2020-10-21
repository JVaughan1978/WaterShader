Shader "FEMA_Water/ShapeCombine"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }
        SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;

            #define SHAPE_LOD_PARAMS(LODNUM) \
            uniform sampler2D _WD_Sampler_##LODNUM; \
            uniform sampler2D _WD_WaterDepth_Sampler_##LODNUM; \
            uniform float3 _WD_Params_##LODNUM; \
            uniform float2 _WD_Pos_##LODNUM; \
            uniform int _WD_LodIdx_##LODNUM;

            SHAPE_LOD_PARAMS(0)
            SHAPE_LOD_PARAMS(1)

            float2 depthWorldToUV(in float2 i_samplePos, in float2 i_centerPos, in float i_res, in float i_texelSize)
            {
                return (i_samplePos - i_centerPos) / (i_texelSize*i_res) + 0.5;
            }

            float2 depthUVToWorld(in float2 i_uv, in float2 i_centerPos, in float i_res, in float i_texelSize)
            {
                return i_texelSize * i_res * (i_uv - 0.5) + i_centerPos;
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 worldPos = depthUVToWorld(i.uv, _WD_Pos_0, _WD_Params_0.y, _WD_Params_0.x);
                float2 uv_1 = depthWorldToUV(worldPos, _WD_Pos_1, _WD_Params_1.y, _WD_Params_1.x);
                return tex2D(_MainTex, uv_1);
            }
            ENDCG
        }
    }
}

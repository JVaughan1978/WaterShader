Shader "FEMA_Water/WaterDepth"
{
    Properties{}

    Category
    {
        Tags { "Queue" = "Geometry" }

        SubShader
        {
            Pass
            {
                Name "BASE"
                Tags { "LightMode" = "Always" }
                BlendOp Min

                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 4.0
                #pragma multi_compile_fog
                #include "UnityCG.cginc"

                uniform float _SeaLevel;
                #define DEPTH_BIAS 100.

                struct appdata_t
                {
                    float4 vertex : POSITION;
                };

                struct v2f
                {
                    float4 vertex : SV_POSITION;
                    half depth : TEXCOORD0;
                };

                v2f vert(appdata_t v)
                {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    half altitude = mul(unity_ObjectToWorld, v.vertex).y;
                    o.depth = _SeaLevel - altitude - DEPTH_BIAS;
                    return o;
                }

                half frag(v2f i) : SV_Target
                {
                    return i.depth;
                }

                ENDCG
            }
        }
    }
}

Shader "Custom/Refractor" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		//_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
        _Refraction("Refraction Amount", float) = 0.1
	}
	SubShader {
		Tags { "RenderType"="Transparent" "Queue" = "Transparent" }
		LOD 200
        GrabPass { "_GrabTexture" }
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows vertex:vert

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		//sampler2D _MainTex;
        sampler2D _GrabTexture;

		struct Input {
            float4 grabUV;
            float4 refract;
		};

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;
        float _Refraction;

        void vert(inout appdata_full i, out Input o)
        {
            float4 pos = UnityObjectToClipPos(i.vertex);
            o.grabUV = ComputeGrabScreenPos(pos);
            o.refract = float4(i.normal,0) * _Refraction;
        }

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_BUFFER_END(Props)

		void surf (Input IN, inout SurfaceOutputStandard o) {
			// Albedo comes from a texture tinted by color
            fixed4 c = tex2Dproj(_GrabTexture, UNITY_PROJ_COORD(IN.grabUV + IN.refract));//  *_Color;
			o.Emission = c.rgb;
			// Metallic and smoothness come from slider variables
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}

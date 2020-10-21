// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'

Shader "FEMA_Water/Water"
{
    Properties
    {
        _Distance("Draw Distance", Range(3.0, 256.0)) = 30.0
        _RefractionFalloff("Refraction Falloff", Range(0,1)) = 0
        [NoScaleOffset] _Normals("Normals", 2D) = "bump" {}
        _NormalsStrength("Normals Strength", Range(0.0, 2.0)) = 0.3
        _NormalsScale("Normals Scale", Range(0.0, 50.0)) = 1.0
        [NoScaleOffset] _Skybox("Skybox", CUBE) = "" {}
        _Diffuse("Diffuse", Color) = (0.2, 0.05, 0.05, 1.0)
        _DirectionalLightFallOff("Directional Light Fall-Off", Range(1.0, 512.0)) = 128.0
        _DirectionalLightBoost("Directional Light Boost", Range(0.0, 16.0)) = 5.0
        [Toggle] _SubSurfaceScattering("Sub-Surface Scattering", Float) = 1
        _SubSurfaceColour("Sub-Surface Scattering Colour", Color) = (0.0, 0.48, 0.36, 1.)
        _SubSurfaceBase("Sub-Surface Scattering Base Mul", Range(0.0, 2.0)) = 0.6
        _SubSurfaceSun("Sub-Surface Scattering Sun Mul", Range(0.0, 10.0)) = 0.8
        _SubSurfaceSunFallOff("Sub-Surface Scattering Sun Fall-Off", Range(1.0, 16.0)) = 4.0
        [Toggle] _Foam("Foam", Float) = 1
        [NoScaleOffset] _FoamTexture("Foam Texture", 2D) = "white" {}
        _FoamScale("Foam Scale", Range(0.0, 50.0)) = 10.0
        _FoamWhiteColor("White Foam Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _FoamBubbleColor("Bubble Foam Color", Color) = (0.0, 0.0904, 0.105, 1.0)
        _ShorelineFoamMinDepth("Shoreline Foam Min Depth", Range(0.0, 5.0)) = 0.27
        _ShorelineFoamMaxDepth("Shoreline Foam Max Depth", Range(0.0,10.0)) = 1.5
        _WaveFoamCoverage("Wave Foam Coverage", Range(0.0,5.0)) = 0.95
        _WaveFoamStrength("Wave Foam Strength", Range(0.0,10.0)) = 2.8
        _WaveFoamFeather("Wave Foam Feather", Range(0.001,1.0)) = 0.32
        _WaveFoamBubblesCoverage("Wave Foam Bubbles Coverage", Range(0.0,5.0)) = 0.95
        _WaveFoamLightScale("Wave Foam Light Scale", Range(0.0, 2.0)) = 0.7
        _WaveFoamNormalsY("Wave Foam Normal Y Component", Range(0.0, 0.5)) = 0.5
        _DepthFogDensity("Depth Fog Density", Vector) = (0.28, 0.16, 0.24, 1.0)
        _FresnelPower("Fresnel Power", Range(0.0,20.0)) = 3.0
    }

    Category
    {
        SubShader
        {
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            GrabPass {"_grabpass"}
            Pass
            {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 4.0
                #pragma multi_compile_fog
                #pragma shader_feature _SUBSURFACESCATTERING_ON
                #pragma shader_feature _TRANSPARENCY_ON
                #pragma shader_feature _FOAM_ON
                #include "UnityCG.cginc"

                #define BLEND_WIDTH 0.4
                #define vec2 half2
                #define vec3 half3
                #define vec4 half4
                #define fract frac
                #define mod fmod
                #define texture tex2D
                #define mix lerp
                #define iTime _Time.y

                vec4 hash4(vec2 p) { return fract(sin(vec4(1.0 + dot(p,vec2(37.0,17.0)), 2.0 + dot(p,vec2(11.0,47.0)), 3.0 + dot(p,vec2(41.0,29.0)), 4.0 + dot(p,vec2(23.0,31.0))))*103.); }
                vec2 transformUVs(in vec2 iuvCorner, in vec2 uv)
                {
                    vec4 tx = hash4(iuvCorner);
                    tx.zw = sign(tx.zw - 0.5);
                    return tx.zw * uv + tx.xy;
                }

                vec4 textureNoTile_3weights(sampler2D samp, in vec2 uv)
                {
                    vec4 res = (vec4)(0.0);
                    int sampleCnt = 0;
                    vec2 fuv = mod(uv, 2.);
                    vec2 iuv = uv - fuv;
                    vec3 BL_one = vec3(0.,0.,1.);
                    if (fuv.x >= 1.) fuv.x = 2. - fuv.x, BL_one.x = 2.;
                    if (fuv.y >= 1.) fuv.y = 2. - fuv.y, BL_one.y = 2.;

                    vec2 iuv3;
                    float w3 = (fuv.x + fuv.y) - 1.;
                    if (w3 < 0.) iuv3 = iuv + BL_one.xy, w3 = -w3; 
                    else iuv3 = iuv + BL_one.zz;
                    w3 = smoothstep(BLEND_WIDTH, 1. - BLEND_WIDTH, w3);

                    if (w3 < 0.999)
                    {
                        float w12 = dot(fuv,vec2(.5,-.5)) + .5;
                        w12 = smoothstep(1.125*BLEND_WIDTH, 1. - 1.125*BLEND_WIDTH, w12);
                        
                        if (w12 > 0.001) res += w12 * texture(samp, transformUVs(iuv + BL_one.zy, uv)), sampleCnt++;
                        if (w12 < 0.999) res += (1. - w12) * texture(samp, transformUVs(iuv + BL_one.xz, uv)), sampleCnt++;
                    }
                    if (w3 > 0.001) res = mix(res, texture(samp, transformUVs(iuv3, uv)), w3), sampleCnt++;
                    return res;
                }

                #define DEPTH_BIAS 100.0

                struct appdata_t
                {
                    float4 vertex : POSITION;
                    float2 texcoord: TEXCOORD0;
                };

                struct v2f
                {
                    float4 vertex : SV_POSITION;
                    half3 normal : TEXCOORD1;
                    half4 shorelineFoam_screenPos : TEXCOORD4;
                    half4 world_XZ_undisplaced : TEXCOORD5;
                    float3 worldPos : TEXCOORD7;
                    half4 grabPos : TEXCOORD9;

                    UNITY_FOG_COORDS(3)
                };

                #define SHAPE_LOD_PARAMS(LODNUM) \
                uniform sampler2D _WD_Sampler_##LODNUM; \
                uniform sampler2D _WD_WaterDepth_Sampler_##LODNUM; \
                uniform float3 _WD_Params_##LODNUM; \
                uniform float2 _WD_Pos_##LODNUM; \
                uniform int _WD_LodIdx_##LODNUM;
                SHAPE_LOD_PARAMS(0)
                SHAPE_LOD_PARAMS(1)

                float2 WD_worldToUV(in float2 i_samplePos, in float2 i_centerPos, in float i_res, in float i_texelSize)
                {
                    return (i_samplePos - i_centerPos) / (i_texelSize*i_res) + 0.5;
                }

                float2 WD_uvToWorld(in float2 i_uv, in float2 i_centerPos, in float i_res, in float i_texelSize)
                {
                    return i_texelSize * i_res * (i_uv - 0.5) + i_centerPos;
                }

                uniform float3 _WaterCenterPosWorld;
                uniform half _ShorelineFoamMaxDepth;
                uniform float4 _GeomData;
                uniform float4 _InstanceData;

                void SampleDisplacements(in sampler2D i_dispSampler, in sampler2D i_WaterDepthSampler, in float2 i_centerPos, in float i_res, in float i_texelSize, in float i_geomSquareSize, in float2 i_samplePos, in float wt, inout float3 io_worldPos, inout float3 io_n, inout float io_determinant, inout half io_shorelineFoam)
                {
                    if (wt < 0.001)
                        return;

                    float4 uv = float4(WD_worldToUV(i_samplePos, i_centerPos, i_res, i_texelSize), 0., 0.);
                    float3 dd = float3(i_geomSquareSize / (i_texelSize*i_res), 0.0, i_geomSquareSize);
                    float4 s = tex2Dlod(i_dispSampler, uv);
                    float4 sx = tex2Dlod(i_dispSampler, uv + dd.xyyy);
                    float4 sz = tex2Dlod(i_dispSampler, uv + dd.yxyy);
                    float3 disp = s.xyz;
                    float3 disp_x = dd.zyy + sx.xyz;
                    float3 disp_z = dd.yyz + sz.xyz;
                    io_worldPos += wt * disp;

                    float3 n = normalize(cross(disp_z - disp, disp_x - disp));
                    io_n.xz += wt * n.xz;
                    float4 du = float4(disp_x.xz, disp_z.xz) - disp.xzxz;
                    float det = (du.x * du.w - du.y * du.z) / (dd.z * dd.z);
                    det = 1. - det;
                    io_determinant += wt * det;
                    half signedDepth = (tex2Dlod(i_WaterDepthSampler, uv).x + DEPTH_BIAS) + disp.y;
                    io_shorelineFoam += wt * clamp(1. - signedDepth / _ShorelineFoamMaxDepth, 0., 1.);
                }

                sampler2D _grabpass;
                v2f vert(appdata_t v)
                {
                    v2f o;
                    const float SQUARE_SIZE = _GeomData.x, SQUARE_SIZE_2 = 2.0*_GeomData.x, SQUARE_SIZE_4 = 4.0*_GeomData.x;
                    const float BASE_DENSITY = _GeomData.w;
                    o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                    o.worldPos.xz -= frac(_WaterCenterPosWorld.xz / SQUARE_SIZE_2) * SQUARE_SIZE_2; 

                    float2 offsetFromCenter = float2(abs(o.worldPos.x - _WaterCenterPosWorld.x), abs(o.worldPos.z - _WaterCenterPosWorld.z));
                    float taxicab_norm = max(offsetFromCenter.x, offsetFromCenter.y);
                    float idealSquareSize = taxicab_norm / BASE_DENSITY;
                    idealSquareSize = max(idealSquareSize, 0.03125);

                    float lodAlpha = idealSquareSize / SQUARE_SIZE - 1.0;
                    const float BLACK_POINT = 0.15, WHITE_POINT = 0.85;
                    lodAlpha = max((lodAlpha - BLACK_POINT) / (WHITE_POINT - BLACK_POINT), 0.0);
                    const float meshScaleLerp = _InstanceData.x;
                    lodAlpha = min(lodAlpha + meshScaleLerp, 1.);
                    o.world_XZ_undisplaced.y = lodAlpha;
                    float2 m = frac(o.worldPos.xz / SQUARE_SIZE_4);
                    float2 offset = m - 0.5;
                    const float minRadius = 0.26; 
                    if (abs(offset.x) < minRadius) o.worldPos.x += offset.x * lodAlpha * SQUARE_SIZE_4;
                    if (abs(offset.y) < minRadius) o.worldPos.z += offset.y * lodAlpha * SQUARE_SIZE_4;
                    o.world_XZ_undisplaced.zw = o.worldPos.xz;

                    o.normal = half3(0, 1.0, 0);
                    o.normal = normalize(mul(o.normal, unity_WorldToObject));
                    o.world_XZ_undisplaced.x = 0.0;
                    o.shorelineFoam_screenPos.x = 0.0;
                    float wt_0 = (1. - lodAlpha) * _WD_Params_0.z;
                    float wt_1 = (1. - wt_0) * _WD_Params_1.z;
                    const float2 wxz = o.worldPos.xz;
                    SampleDisplacements(_WD_Sampler_0, _WD_WaterDepth_Sampler_0, _WD_Pos_0, _WD_Params_0.y, _WD_Params_0.x, idealSquareSize,
                                        wxz, wt_0, o.worldPos, o.normal, o.world_XZ_undisplaced.x, o.shorelineFoam_screenPos.x);
                    SampleDisplacements(_WD_Sampler_1, _WD_WaterDepth_Sampler_1, _WD_Pos_1, _WD_Params_1.y, _WD_Params_1.x, idealSquareSize,
                                        wxz, wt_1, o.worldPos, o.normal, o.world_XZ_undisplaced.x, o.shorelineFoam_screenPos.x);
                    o.vertex = mul(UNITY_MATRIX_VP, float4(o.worldPos, 1.0));

                    UNITY_TRANSFER_FOG(o, o.vertex);
                    o.grabPos = ComputeGrabScreenPos(o.vertex);
                    o.shorelineFoam_screenPos.yzw = ComputeScreenPos(o.vertex).xyw;
                    return o;
                }

                uniform half _Distance;
                uniform half _RefractionFalloff;
                uniform half4 _Diffuse;
                uniform half _DirectionalLightFallOff;
                uniform half _DirectionalLightBoost;
                uniform half4 _SubSurfaceColour;
                uniform half _SubSurfaceBase;
                uniform half _SubSurfaceSun;
                uniform half _SubSurfaceSunFallOff;
                uniform half4 _DepthFogDensity;
                uniform samplerCUBE _Skybox;
                uniform sampler2D _FoamTexture;
                uniform float4 _FoamTexture_TexelSize;
                uniform half4 _FoamWhiteColor;
                uniform half4 _FoamBubbleColor;
                uniform half _ShorelineFoamMinDepth;
                uniform half _WaveFoamStrength, _WaveFoamCoverage, _WaveFoamFeather;
                uniform half _WaveFoamBubblesCoverage;
                uniform half _WaveFoamNormalsY;
                uniform half _WaveFoamLightScale;
                uniform sampler2D _Normals;
                uniform half _NormalsStrength;
                uniform half _NormalsScale;
                uniform half _FoamScale;
                uniform half _FresnelPower;
                uniform float _CurrentTime;
                uniform fixed4 _LightColor0;
                uniform half2 _WindDirXZ;

                sampler2D _BackgroundTexture;
                sampler2D _CameraDepthTexture;

                void ApplyNormalMaps(float2 worldXZUndisplaced, float lodAlpha, inout half3 io_n)
                {
                    const float2 v0 = float2(0.94, 0.34), v1 = float2(-0.85, -0.53);
                    const float geomSquareSize = _GeomData.x;
                    float nstretch = _NormalsScale * geomSquareSize;
                    const float spdmulL = _GeomData.y;
                    half2 norm =
                        UnpackNormal(tex2D(_Normals, (v0*_CurrentTime*spdmulL + worldXZUndisplaced) / nstretch)).xy +
                        UnpackNormal(tex2D(_Normals, (v1*_CurrentTime*spdmulL + worldXZUndisplaced) / nstretch)).xy;

                    const float farNormalsWeight = _InstanceData.y;
                    const half nblend = lodAlpha * farNormalsWeight;
                    if (nblend > 0.001)
                    {
                        nstretch *= 2.;
                        const float spdmulH = _GeomData.z;
                        norm = lerp(norm,
                            UnpackNormal(tex2D(_Normals, (v0*_CurrentTime*spdmulH + worldXZUndisplaced) / nstretch)).xy +
                            UnpackNormal(tex2D(_Normals, (v1*_CurrentTime*spdmulH + worldXZUndisplaced) / nstretch)).xy,
                            nblend);
                    }
                    io_n.xz += _NormalsStrength * norm;
                    io_n = normalize(io_n);
                }

                half3 AmbientLight()
                {
                    return half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);
                }

                void ComputeFoam(half i_determinant, float2 i_worldXZUndisplaced, float2 i_worldXZ, half3 i_n, half i_shorelineFoam, float i_pixelZ, float i_sceneZ, half3 i_view, float3 i_lightDir, out half3 o_bubbleCol, out half4 o_whiteFoamCol)
                {
                    half foamAmount = 0.;
                    foamAmount += _WaveFoamStrength * saturate(_WaveFoamCoverage - i_determinant);
                    foamAmount += i_shorelineFoam;
                    foamAmount *= saturate((i_sceneZ - i_pixelZ) / _ShorelineFoamMinDepth);

                    float2 foamUVBubbles = (lerp(i_worldXZUndisplaced, i_worldXZ, 0.5) + 0.5 * _CurrentTime * _WindDirXZ) / _FoamScale;
                    foamUVBubbles += 0.25 * i_n.xz;
                    half bubbleFoamTexValue = texture(_FoamTexture, .37 * foamUVBubbles - .1*i_view.xz / i_view.y).r;
                    half bubbleFoam = bubbleFoamTexValue * saturate(_WaveFoamBubblesCoverage - i_determinant);
                    o_bubbleCol = bubbleFoam * _FoamBubbleColor.rgb * (AmbientLight() + _LightColor0);

                    float2 foamUV = (i_worldXZUndisplaced + 0.5 * _CurrentTime * _WindDirXZ) / _FoamScale + 0.02 * i_n.xz;
                    half foamTexValue = texture(_FoamTexture, foamUV).r;
                    half whiteFoam = foamTexValue * (smoothstep(foamAmount + _WaveFoamFeather, foamAmount, 1. - foamTexValue)) * _FoamWhiteColor.a;

                    float2 dd = float2(0.25 * i_pixelZ * _FoamTexture_TexelSize.x, 0.);
                    half foamTexValue_x = texture(_FoamTexture, foamUV + dd.xy).r;
                    half foamTexValue_z = texture(_FoamTexture, foamUV + dd.yx).r;
                    half whiteFoam_x = foamTexValue_x * (smoothstep(foamAmount + _WaveFoamFeather, foamAmount, 1. - foamTexValue_x)) * _FoamWhiteColor.a;
                    half whiteFoam_z = foamTexValue_z * (smoothstep(foamAmount + _WaveFoamFeather, foamAmount, 1. - foamTexValue_z)) * _FoamWhiteColor.a;
                    half sqrt_foam = sqrt(whiteFoam);
                    half dfdx = sqrt(whiteFoam_x) - sqrt_foam, dfdz = sqrt(whiteFoam_z) - sqrt_foam;
                    half3 fN = normalize(half3(-dfdx, _WaveFoamNormalsY, -dfdz));
                    half foamNdL = max(0., dot(fN, i_lightDir));
                    o_whiteFoamCol.rgb = _FoamWhiteColor.rgb * (AmbientLight() + _WaveFoamLightScale * _LightColor0 * foamNdL);
                    o_whiteFoamCol.a = min(2. * whiteFoam, 1.);
                }

                float3 WorldSpaceLightDir(float3 worldPos)
                {
                    float3 lightDir = _WorldSpaceLightPos0.xyz;
                    if (_WorldSpaceLightPos0.w > 0.)
                    {
                        lightDir = normalize(lightDir - worldPos.xyz);
                    }
                    return lightDir;
                }

                half3 WaterEmission(half3 view, half3 n, half3 n_geom, float3 lightDir, half4 grabPos, half3 screenPos, float pixelZ, half2 uvDepth, float sceneZ, half3 bubbleCol)
                {
                    half3 col = _Diffuse.rgb * half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);

                    #if _SUBSURFACESCATTERING_ON
                    half towardsSun = pow(max(0., dot(lightDir, -view)), _SubSurfaceSunFallOff);
                    col += (_SubSurfaceBase + _SubSurfaceSun * towardsSun) * max(dot(n_geom, view), 0.) * _SubSurfaceColour * _LightColor0;
                    #endif

                    col += bubbleCol;
                    half2 uvBackgroundRefract = grabPos.xy / grabPos.w + .02 * n.xz;
                    half2 uvDepthRefract = uvDepth + .02 * n.xz;
                    half3 alpha = (half3)1.;
                    if (sceneZ > pixelZ)
                    {
                        float sceneZRefract = LinearEyeDepth(texture(_CameraDepthTexture, uvDepthRefract).x);
                        float maxZ = max(sceneZ, sceneZRefract);
                        float deltaZ = maxZ - pixelZ;
                        alpha = 1. - exp(-_DepthFogDensity.xyz * deltaZ);
                    }
                    half3 sceneColour = texture(_BackgroundTexture, uvBackgroundRefract).rgb;
                    col = lerp(sceneColour, col, alpha);
                    return col;
                }

                half4 frag(v2f i) : SV_Target
                {
                    half3 view = normalize(_WorldSpaceCameraPos - i.worldPos);
                    float pixelZ = LinearEyeDepth(i.vertex.z);
                    half3 screenPos = i.shorelineFoam_screenPos.yzw;
                    half2 uvDepth = screenPos.xy / screenPos.z;
                    float sceneZ = LinearEyeDepth(texture(_CameraDepthTexture, uvDepth).x);

                    float3 lightDir = WorldSpaceLightDir(i.worldPos);

                    half3 n_geom = normalize(i.normal);
                    half3 n_pixel = n_geom;
                    ApplyNormalMaps(i.world_XZ_undisplaced.zw, i.world_XZ_undisplaced.y, n_pixel);

                    half3 bubbleCol = (half3)0.;
                    #if _FOAM_ON
                    half4 whiteFoamCol;
                    ComputeFoam(1.0 - i.world_XZ_undisplaced.x, i.world_XZ_undisplaced.zw, i.worldPos.xz, n_pixel, i.shorelineFoam_screenPos.x, pixelZ, sceneZ, view, lightDir, bubbleCol, whiteFoamCol);
                    #endif

                    half4 col = _Diffuse;
                    half4 refraction = tex2Dproj(_grabpass, UNITY_PROJ_COORD(i.grabPos + half4(0.0, 1.0, 0.0, 0.0)));
                    //return half4(refraction.rgb, 1.0);

                    col.rgb = WaterEmission(view, n_pixel, n_geom, lightDir, refraction, screenPos, pixelZ, uvDepth, sceneZ, bubbleCol);
                    half3 refl = reflect(-view, n_pixel);
                    half3 skyColor = texCUBE(_Skybox, refl);
                    skyColor += refraction * _RefractionFalloff;
                    skyColor += pow(max(0.0, dot(refl, lightDir)), _DirectionalLightFallOff) * _DirectionalLightBoost * _LightColor0;

                    const float IOR_AIR = 1.0;
                    const float IOR_WATER = 1.33;
                    float R_0 = (IOR_AIR - IOR_WATER) / (IOR_AIR + IOR_WATER); //R_0 *= R_0;
                    float R_theta = R_0 + (1.0 - R_0) * pow(1.0 - max(dot(n_pixel, view), 0.), _FresnelPower);
                    col.rgb = lerp(col.rgb, skyColor, R_theta);
                    #if _FOAM_ON
                    col.rgb = lerp(col, whiteFoamCol.rgb, whiteFoamCol.a);
                    #endif
                    float dist = distance(i.worldPos, _WorldSpaceCameraPos);
                    col.a *= saturate(1.0-(dist / (_Distance * 3.0)));
                    UNITY_APPLY_FOG(i.fogCoord, col);
                    return col;
                }
                ENDCG
            }
        }
    }
}

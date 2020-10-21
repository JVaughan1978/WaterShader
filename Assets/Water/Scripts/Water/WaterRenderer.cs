using System.Collections.Generic;
using UnityEngine;

namespace FEMA_AR.WATER
{
    public class WaterRenderer : MonoBehaviour
    {
        public Camera arCamera = null;
        public Material waterMaterial;

        //TODO: hide in inspector and setup with custom drawer
        public Shader shapeCombineMaterial;
        public Shader waterDepthMaterial;

        [HideInInspector] public WaterBuilder builder;
        public float viewerAltitudeLevel;
        public float maxHorizDisplacement;
        public float maxVertDisplacement;
        float maxHorizDispFromShape;
        float maxVertDispFromShape;

        public float minScale = 16f;
        public float maxScale = 128f;
        public float minTexelsPerWave = 5f;
        public float baseVertDensity = 32f;
        public int lodCount = 7;

        [HideInInspector] public HashSet<WaterDepthRenderable> waterDepthRenderables = new HashSet<WaterDepthRenderable>();

        [Tooltip("Wind direction (angle from x axis in degrees)"), Range(-180, 180)]
        public float windDirectionAngle = 0f;
        [Tooltip("Wind speed in m/s"), Range(0, 20), HideInInspector]
        public float windSpeed = 5f;
        public Vector2 WindDir { get { return new Vector2(Mathf.Cos(Mathf.PI * windDirectionAngle / 180f), Mathf.Sin(Mathf.PI * windDirectionAngle / 180f)); } }

        float deltaTime = 0f;
        float elapsedTime = 0f;
        public float ElapsedTime { get { return elapsedTime; } }
        public float XScale { get { return transform.localScale.x; } }
        public float SeaLevel { get { return transform.position.y; } }

        int maxDisplacementCachedTime;
        public void GetMaxDisplacement(float maxHorizDisp, float maxVertDisp)
        {
            if (Time.frameCount != maxDisplacementCachedTime)
            {
                maxHorizDispFromShape = maxVertDispFromShape = 0f;
            }
            maxHorizDispFromShape += maxHorizDisp;
            maxVertDispFromShape += maxVertDisp;
            maxDisplacementCachedTime = Time.frameCount;
        }

        void Start()
        {
            builder = new WaterBuilder();
            builder.waterMat = waterMaterial;
            builder.GenerateMesh(transform, baseVertDensity, lodCount, shapeCombineMaterial, waterDepthMaterial);
            if(arCamera == null )  arCamera = Camera.main;
        }

        void OnEnable()
        {
            WaterDepthRenderable.OnUpdated += SetDepthDirty;
        }

        void OnDisable()
        {
            WaterDepthRenderable.OnUpdated -= SetDepthDirty;
        }

        bool depthSet = false;
        void SetDepthDirty(WaterDepthRenderable wdr)
        {
            waterDepthRenderables.Add(wdr);

            if (builder == null || builder.waveCams.Length < 1)
            {
                depthSet = false;
                return;
            }

            foreach (WaveCam wc in builder.waveCams)
            {
                wc.SetDepthRendererDirty();
            }
            depthSet = true;
        }

        void Update()
        {
            if (!depthSet)
            {
                foreach (WaterDepthRenderable wdr in waterDepthRenderables)
                {
                    SetDepthDirty(wdr);
                }
            }

            if (builder != null)
            {
                for (int wcr = 0;
                    wcr < builder.waterChunkRenderers.Count;
                    wcr++)
                {
                    if(builder.waterChunkRenderers[wcr] != null)
                        builder.waterChunkRenderers[wcr].UpdateMeshBounds(maxHorizDisplacement, maxVertDisplacement);
                }
            }
        }

        void LateUpdate()
        {
            deltaTime = Time.deltaTime;
            elapsedTime += deltaTime;

            // set global shader params
            Shader.SetGlobalFloat("_CurrentTime", elapsedTime);
            Shader.SetGlobalFloat("_DeltaTime", deltaTime);
            Shader.SetGlobalFloat("_TexelsPerWave", minTexelsPerWave);
            Shader.SetGlobalVector("_WindDirXZ", WindDir);
            Shader.SetGlobalFloat("_SeaLevel", SeaLevel);

            //set position
            if (arCamera)
            {
                Vector3 pos = arCamera.transform.position;
                pos.y = transform.position.y;
                transform.position = pos;
                Shader.SetGlobalVector("_WaterCenterPosWorld", transform.position);

                float maxDetailY = SeaLevel - maxVertDispFromShape / 5f;
                float camY = Mathf.Max(arCamera.transform.position.y - maxDetailY, 0f);

                const float HEIGHT_LOD_MUL = 2f;
                float level = camY * HEIGHT_LOD_MUL;
                level = Mathf.Max(level, minScale);
                if (maxScale != -1f)
                {
                    level = Mathf.Min(level, 1.99f * maxScale);
                }

                float l2 = Mathf.Log(level) / Mathf.Log(2f);
                float l2f = Mathf.Floor(l2);

                viewerAltitudeLevel = l2 - l2f;

                float newScale = Mathf.Pow(2f, l2f);
                transform.localScale = new Vector3(newScale, 1f, newScale);

                float maxWaveLength =
                    builder.waveCams[builder.waveCams.Length - 1].MaxWaveLength(XScale, baseVertDensity, minTexelsPerWave);
                Shader.SetGlobalFloat("_MaxWaveLength", maxWaveLength);
                Shader.SetGlobalFloat("_ViewerAltitude", viewerAltitudeLevel);
            }
        }
    }
}

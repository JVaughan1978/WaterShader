using UnityEngine;
using UnityEngine.Rendering;

namespace FEMA_AR.WATER
{
    public class WaveCam : MonoBehaviour
    {
        public int lodIndex;
        public int lodCount;
        [HideInInspector] public WaterRenderer waterRenderer = null;
        [HideInInspector] public Material matCombineShapes = null;
        [HideInInspector] public Material matWaterDepth = null;

        Camera cam;
        CommandBuffer cbCombineShapes = null;
        RenderTexture rtWaterDepth;
        CommandBuffer cbWaterDepth;

        bool depthRenderersDirty = true;
        int resolution = -1;

        public struct RenderData
        {
            public float texelWidth;
            public float textureRes;
            public Vector3 posSnapped;
            public Vector3 posSnappedLast;
        }
        public RenderData renderData = new RenderData();
        
        void Start()
        {
            cam = GetComponent<Camera>();
        }

        void Update()
        {
            //COMBINE SHAPES
            renderData.posSnappedLast = renderData.posSnapped;
            if (lodIndex == 0)
            {
                if (cbCombineShapes == null)
                {
                    cbCombineShapes = new CommandBuffer();
                    cam.AddCommandBuffer(CameraEvent.AfterEverything, cbCombineShapes);
                    cbCombineShapes.name = "Combine Shapes";

                    Camera[] cams = waterRenderer.builder.shapeCams;
                    for (int combineCam = cams.Length - 2; combineCam >= 0; combineCam--)
                    {
                        Material matCombine = waterRenderer.builder.waveCams[combineCam].matCombineShapes;
                        cbCombineShapes.Blit(cams[combineCam + 1].targetTexture, cams[combineCam].targetTexture, matCombine);
                    }
                }
            }

            if (depthRenderersDirty)
            {
                //Scales wave height based on proximity to underlying geometry.
                WaterDepthRenderable[] wdrs = new WaterDepthRenderable[waterRenderer.waterDepthRenderables.Count];
                int incrementor = 0;
                foreach (WaterDepthRenderable wdr in waterRenderer.waterDepthRenderables)
                {
                    wdrs[incrementor] = wdr;
                    incrementor++;
                }
                
                // if there is nothing in the scene tagged up for depth rendering then there is no depth rendering required
                if (wdrs.Length < 1)
                {
                    if (cbWaterDepth != null)
                        cbWaterDepth.Clear();
                    return;
                }

                if (!rtWaterDepth)
                {
                    rtWaterDepth = new RenderTexture(cam.targetTexture.width, cam.targetTexture.height, 0);
                    rtWaterDepth.name = gameObject.name + "_waterDepth";
                    rtWaterDepth.format = RenderTextureFormat.RHalf;
                    rtWaterDepth.useMipMap = false;
                    rtWaterDepth.anisoLevel = 0;
                }

                if (cbWaterDepth == null)
                {
                    cbWaterDepth = new CommandBuffer();
                    cam.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, cbWaterDepth);
                    cbWaterDepth.name = "Water Depth";
                }

                cbWaterDepth.Clear();
                cbWaterDepth.SetRenderTarget(rtWaterDepth);
                cbWaterDepth.ClearRenderTarget(false, true, Color.red * 10000f);

                foreach(WaterDepthRenderable wdr in wdrs)
                {
                    if (!wdr.enabled)
                        continue;
                    var r = wdr.GetComponent<Renderer>();
                    if (r == null)
                        Debug.LogError("GameObject " + wdr.gameObject.name + 
                            " must have a renderer component attached. " +
                            "Unity Terrain objects are not supported", wdr);

                    cbWaterDepth.DrawRenderer(r, matWaterDepth);
                }
                depthRenderersDirty = false;
            }
        }

        void LateUpdate()
        {
            cam.orthographicSize = 2f * transform.lossyScale.x;
            int width = cam.targetTexture.width;
            if (resolution == -1)
            {
                resolution = width;
            }
            else if (width != resolution)
            {
                cam.targetTexture.Release();
                cam.targetTexture.width = cam.targetTexture.height = resolution;
                cam.targetTexture.Create();
            }
            renderData.textureRes = (float)cam.targetTexture.width;
            renderData.texelWidth = 2f * cam.orthographicSize / renderData.textureRes;
            renderData.posSnapped = transform.position
                                    -new Vector3(Mathf.Repeat(transform.position.x, renderData.texelWidth), 
                                                 0f, 
                                                 Mathf.Repeat(transform.position.z, renderData.texelWidth));

            cam.ResetProjectionMatrix();
            Matrix4x4 ProjectionMatrix = cam.projectionMatrix;
            Matrix4x4 Transformation = new Matrix4x4();
            Transformation.SetTRS(new Vector3(transform.position.x - renderData.posSnapped.x, 
                                              transform.position.z - renderData.posSnapped.z,
                                              0f), 
                                 Quaternion.identity, 
                                 Vector3.one);
            ProjectionMatrix *= Transformation;
            cam.projectionMatrix = ProjectionMatrix;

            ApplyMaterialParams(0, matCombineShapes, true, true);
            if (lodIndex > 0)
            {
                ApplyMaterialParams(1, waterRenderer.builder.waveCams[lodIndex - 1].matCombineShapes, true, true);
            }
        }

        void OnEnable()
        {
            RemoveCommandBuffers();
        }

        void OnDisable()
        {
            RemoveCommandBuffers();
        }

        void RemoveCommandBuffers()
        {
            if (cbWaterDepth != null)
            {
                cam.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, cbWaterDepth);
                cbWaterDepth = null;
            }

            if (cbCombineShapes != null)
            {
                cam.RemoveCommandBuffer(CameraEvent.AfterEverything, cbCombineShapes);
                cbCombineShapes = null;
            }
        }

        public void ApplyMaterialParams(int shapeSlot, Material properties, bool applyWaveHeights, bool blendOut)
        {
            if (applyWaveHeights)
                properties.SetTexture("_WD_Sampler_" + shapeSlot.ToString(), cam.targetTexture);

            if (rtWaterDepth != null)
                properties.SetTexture("_WD_WaterDepth_Sampler_" + shapeSlot.ToString(), rtWaterDepth);

            bool needToBlendOutShape = (lodIndex == lodCount - 1) && blendOut;
            float shapeWeight = needToBlendOutShape ? waterRenderer.viewerAltitudeLevel : 1f;
            properties.SetVector("_WD_Params_" + shapeSlot.ToString(), new Vector3(renderData.texelWidth, renderData.textureRes, shapeWeight));
            properties.SetVector("_WD_Pos_" + shapeSlot.ToString(), new Vector2(renderData.posSnapped.x, renderData.posSnapped.z));
            properties.SetFloat("_WD_LodIdx_" + shapeSlot.ToString(), lodIndex);
        }

        public void ApplyMaterialBlockParams(int shapeSlot, MaterialPropertyBlock properties, bool applyWaveHeights, bool blendOut)
        {
            if (applyWaveHeights)
                properties.SetTexture("_WD_Sampler_" + shapeSlot.ToString(), cam.targetTexture);

            if (rtWaterDepth != null)
                properties.SetTexture("_WD_WaterDepth_Sampler_" + shapeSlot.ToString(), rtWaterDepth);

            bool needToBlendOutShape = (lodIndex == lodCount - 1) && blendOut;
            float shapeWeight = needToBlendOutShape ? waterRenderer.viewerAltitudeLevel : 1f;
            properties.SetVector("_WD_Params_" + shapeSlot.ToString(), new Vector3(renderData.texelWidth, renderData.textureRes, shapeWeight));
            properties.SetVector("_WD_Pos_" + shapeSlot.ToString(), new Vector2(renderData.posSnapped.x, renderData.posSnapped.z));
            properties.SetFloat("_WD_LodIdx_" + shapeSlot.ToString(), lodIndex);
        }

        public float MaxWaveLength(float scaleX, float vertDensity, float minTexels)
        {
            float maxDiameter = 4f * scaleX * Mathf.Pow(2f, lodIndex);
            float maxTexelSize = maxDiameter / (4f * vertDensity);
            return 2f * maxTexelSize * minTexels;
        }

        public void SetDepthRendererDirty()
        {
            depthRenderersDirty = true;
        }
    }
}

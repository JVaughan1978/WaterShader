using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FEMA_AR.WATER
{
    public class WaterBuilder
    {
        enum PatchType
        {
            Interior,
            Extra,
            ExtraX,
            ExtraXLessZ,
            ExtraXOuter,
            ExtraXZ,
            ExtraXZOuter,
            LessX,
            LessXZ,
            LessXExtraZ,
            Count
        }

        const string SHAPE_RENDER_LAYER = "Waves";
        public Material waterMat;
        public Camera[] shapeCams;
        public WaveCam[] waveCams;
        public List<WaterChunkRenderer> waterChunkRenderers = new List<WaterChunkRenderer>();
        public int CurrentLodCount {get {return shapeCams.Length;} }

        public void GenerateMesh(Transform transform, float baseVertDensity, int lodCount,
                                 Shader shapeCombine, Shader waterDepth)
        {
            if (lodCount < 1)
            {
                Debug.LogError("Invalid LOD count: " + lodCount.ToString());
                return;
            }

            Mesh[] meshInstances = new Mesh[(int)PatchType.Count];
            for (int i = 0; i < (int)PatchType.Count; i++)
            {
                meshInstances[i] = BuildWaterPatch((PatchType)i, baseVertDensity);
            }

            shapeCams = new Camera[lodCount];
            waveCams = new WaveCam[lodCount];
            for (int i = 0; i < lodCount; i++)
            {
                CreateWaveCam(i, lodCount, baseVertDensity, transform.gameObject, 
                              shapeCombine, waterDepth);
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("LOD"))
                {
                    child.parent = null;
                    UnityEngine.Object.Destroy(child.gameObject);
                    i--;
                }
            }

            int startLevel = 0;
            for (int i = 0; i < lodCount; i++)
            {
                bool lastLOD = i == lodCount - 1;
                GameObject nextLod = CreateLOD(transform, i, lodCount, lastLOD, meshInstances, baseVertDensity);
                nextLod.transform.parent = transform;

                float horizScale = Mathf.Pow(2f, (float)(i + startLevel));
                nextLod.transform.localScale = new Vector3(horizScale, 1f, horizScale);
            }
        }

        void CreateWaveCam(int lodIdx, int lodCount, float baseVertDensity, GameObject waveRenderer,
                           Shader shapeCombine, Shader waterDepth)
        {
            GameObject go = new GameObject(string.Format("ShapeCam{0}", lodIdx));

            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Color;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << LayerMask.NameToLayer(SHAPE_RENDER_LAYER);
            cam.orthographic = true;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 500f;
            cam.useOcclusionCulling = false;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.depth = -10 - lodIdx;
            cam.depthTextureMode = DepthTextureMode.None;
            shapeCams[lodIdx] = cam;

            WaveCam wdc = go.AddComponent<WaveCam>();
            wdc.lodIndex = lodIdx;
            wdc.lodCount = lodCount;
            wdc.waterRenderer = waveRenderer.GetComponent<WaterRenderer>();
            Material matShapeCombine = new Material(shapeCombine);
            wdc.matCombineShapes = matShapeCombine;
            Material matWaterDepth = new Material(waterDepth);
            wdc.matWaterDepth = matWaterDepth;
            waveCams[lodIdx] = wdc;

            int size = (int)(4f * baseVertDensity);
            RenderTextureFormat format = RenderTextureFormat.DefaultHDR;
            if(SystemInfo.SupportsRenderTextureFormat(format) == false)
            {
            	format = RenderTextureFormat.ARGBFloat;
            	if(SystemInfo.SupportsRenderTextureFormat(format) == false)
            	{
            		format = RenderTextureFormat.ARGBHalf;
            		if(	SystemInfo.SupportsRenderTextureFormat(format) == false)
            		{
            			format = RenderTextureFormat.Default;
            		}
            	}
            }

            RenderTexture tex = new RenderTexture(size, size, 0, format)
            {
                name = string.Format("shapeRT{0}", lodIdx),
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
                useMipMap = false,
            };
            cam.targetTexture = tex;
        }

        Mesh BuildWaterPatch(PatchType pt, float baseVertDensity)
        {
            ArrayList verts = new ArrayList();
            ArrayList indices = new ArrayList();
            float dx = 1f / baseVertDensity;

            float skirtXminus = 0f, skirtXplus = 0f;
            float skirtZminus = 0f, skirtZplus = 0f;

            switch(pt)
            {
                case PatchType.Extra: skirtXminus = skirtXplus = skirtZminus = skirtZplus = 1f; break;
                case PatchType.ExtraX: skirtXplus = 1f; break;
                case PatchType.ExtraXOuter: skirtXplus = 1f; break;
                case PatchType.ExtraXZ: skirtXplus = skirtZplus = 1f; break;
                case PatchType.ExtraXZOuter: skirtXplus = skirtZplus = 1f; break;
                case PatchType.ExtraXLessZ:  skirtXplus = 1f; skirtZplus = -1f; break;
                case PatchType.LessX: skirtXplus = -1f; break;
                case PatchType.LessXZ: skirtXplus = skirtZplus = -1f; break;
                case PatchType.LessXExtraZ: skirtXplus = -1f; skirtZplus = 1f; break;
                default: break;
            }

            float sideLength_verts_x = 1f + baseVertDensity + skirtXminus + skirtXplus;
            float sideLength_verts_z = 1f + baseVertDensity + skirtZminus + skirtZplus;
            float start_x = -0.5f - skirtXminus * dx;
            float start_z = -0.5f - skirtZminus * dx;
            float end_x = 0.5f + skirtXplus * dx;
            float end_z = 0.5f + skirtZplus * dx;

            for (float j = 0; j < sideLength_verts_z; j++)
            {
                float z = Mathf.Lerp(start_z, end_z, j / (sideLength_verts_z - 1f));

                if (pt == PatchType.ExtraXZOuter && j == sideLength_verts_z - 1f)
                    z *= 100f;

                for (float i = 0; i < sideLength_verts_x; i++)
                {
                    float x = Mathf.Lerp(start_x, end_x, i / (sideLength_verts_x - 1f));
                    if (i == sideLength_verts_x - 1f && (pt == PatchType.ExtraXOuter || pt == PatchType.ExtraXZOuter))
                        x *= 100f;
                    verts.Add(new Vector3(x, 0f, z));
                }
            }

            int sideLength_squares_x = (int)sideLength_verts_x - 1;
            int sideLength_squares_z = (int)sideLength_verts_z - 1;

            for (int j = 0; j < sideLength_squares_z; j++)
            {
                for (int i = 0; i < sideLength_squares_x; i++)
                {
                    bool flipEdge = false;
                    if (i % 2 == 1) flipEdge = !flipEdge;
                    if (j % 2 == 1) flipEdge = !flipEdge;
                    int i0 = i + j * (sideLength_squares_x + 1);
                    int i1 = i0 + 1;
                    int i2 = i0 + (sideLength_squares_x + 1);
                    int i3 = i2 + 1;

                    if (!flipEdge)
                    {
                        indices.Add(i3);//tri 1
                        indices.Add(i1);
                        indices.Add(i0);
                        indices.Add(i0);//tri 2
                        indices.Add(i2);
                        indices.Add(i3);
                    }
                    else
                    {
                        indices.Add(i3);//tri 1
                        indices.Add(i1);
                        indices.Add(i2);
                        indices.Add(i0);//tri 2
                        indices.Add(i2);
                        indices.Add(i1);
                    }
                }
            }

            Mesh mesh = new Mesh();
            if (verts != null && verts.Count > 0)
            {
                Vector3[] arrV = new Vector3[verts.Count];
                verts.CopyTo(arrV);

                int[] arrI = new int[indices.Count];
                indices.CopyTo(arrI);

                mesh.SetIndices(null, MeshTopology.Triangles, 0);
                mesh.vertices = arrV;
                mesh.normals = null;
                mesh.SetIndices(arrI, MeshTopology.Triangles, 0);
                mesh.RecalculateBounds();

                Bounds bounds = mesh.bounds;
                bounds.extents = new Vector3(bounds.extents.x + dx, 100f, bounds.extents.z + dx);

                mesh.bounds = bounds;
                mesh.name = pt.ToString();
            }
            return mesh;
        }

        GameObject CreateLOD(Transform transform, int lodIndex, int lodCount, bool lastLOD, Mesh[] meshData, float baseVertDensity)
        {
            GameObject parent = new GameObject();
            parent.name = "LOD" + lodIndex;
            parent.transform.parent = transform;
            parent.transform.localPosition = Vector3.zero;
            parent.transform.localRotation = Quaternion.identity;

            shapeCams[lodIndex].transform.parent = parent.transform;
            shapeCams[lodIndex].transform.localScale = Vector3.one;
            shapeCams[lodIndex].transform.localPosition = Vector3.up * 100f;
            shapeCams[lodIndex].transform.localEulerAngles = Vector3.right * 90f;

            Vector2[] offsets;
            PatchType[] patchTypes;

            PatchType leadSideType = lastLOD ? PatchType.ExtraXOuter : PatchType.LessX;
            PatchType trailSideType = lastLOD ? PatchType.ExtraXOuter : PatchType.ExtraX;
            PatchType leadCornerType = lastLOD ? PatchType.ExtraXZOuter : PatchType.LessXZ;
            PatchType trailCornerType = lastLOD ? PatchType.ExtraXZOuter : PatchType.ExtraXZ;
            PatchType tlCornerType = lastLOD ? PatchType.ExtraXZOuter : PatchType.LessXExtraZ;
            PatchType brCornerType = lastLOD ? PatchType.ExtraXZOuter : PatchType.ExtraXLessZ;

            if (lodIndex != 0)
            {
                offsets = new Vector2[] {
                    new Vector2(-1.5f,1.5f),  new Vector2(-0.5f,1.5f),  new Vector2(0.5f,1.5f),  new Vector2(1.5f,1.5f),
                    new Vector2(-1.5f,0.5f),                                                     new Vector2(1.5f,0.5f),
                    new Vector2(-1.5f,-0.5f),                                                    new Vector2(1.5f,-0.5f),
                    new Vector2(-1.5f,-1.5f), new Vector2(-0.5f,-1.5f), new Vector2(0.5f,-1.5f), new Vector2(1.5f,-1.5f),
                };

                patchTypes = new PatchType[] {
                    tlCornerType,    leadSideType,  leadSideType,  leadCornerType,
                    trailSideType,                                 leadSideType,
                    trailSideType,                                 leadSideType,
                    trailCornerType, trailSideType, trailSideType, brCornerType,
                };
            }
            else
            {
                offsets = new Vector2[] {
                    new Vector2(-1.5f,1.5f),    new Vector2(-0.5f,1.5f),    new Vector2(0.5f,1.5f),     new Vector2(1.5f,1.5f),
                    new Vector2(-1.5f,0.5f),    new Vector2(-0.5f,0.5f),    new Vector2(0.5f,0.5f),     new Vector2(1.5f,0.5f),
                    new Vector2(-1.5f,-0.5f),   new Vector2(-0.5f,-0.5f),   new Vector2(0.5f,-0.5f),    new Vector2(1.5f,-0.5f),
                    new Vector2(-1.5f,-1.5f),   new Vector2(-0.5f,-1.5f),   new Vector2(0.5f,-1.5f),    new Vector2(1.5f,-1.5f),
                };

                patchTypes = new PatchType[] {
                    tlCornerType,       leadSideType,           leadSideType,           leadCornerType,
                    trailSideType,      PatchType.Interior,     PatchType.Interior,     leadSideType,
                    trailSideType,      PatchType.Interior,     PatchType.Interior,     leadSideType,
                    trailCornerType,    trailSideType,          trailSideType,          brCornerType,
                };
            }

            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject patch = new GameObject(string.Format("Tile_L{0}", lodIndex));
                patch.transform.parent = parent.transform;
                Vector2 pos = offsets[i];
                patch.transform.localPosition = new Vector3(pos.x, 0f, pos.y);
                patch.transform.localScale = Vector3.one;

                WaterChunkRenderer wcr = patch.AddComponent<WaterChunkRenderer>();
                WaterRenderer wr = transform.GetComponent<WaterRenderer>();
                wcr.waterRend = wr;
                wcr.SetInstanceData(lodIndex, lodCount, baseVertDensity);
                waterChunkRenderers.Add(wcr);
                patch.AddComponent<MeshFilter>().mesh = meshData[(int)patchTypes[i]];

                MeshRenderer mr = patch.AddComponent<MeshRenderer>();
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                mr.material = waterMat;

                bool rotateXOutwards = patchTypes[i] == PatchType.ExtraX || patchTypes[i] == PatchType.ExtraXOuter || patchTypes[i] == PatchType.LessX || patchTypes[i] == PatchType.LessXExtraZ;
                if (rotateXOutwards)
                {
                    if (Mathf.Abs(pos.y) >= Mathf.Abs(pos.x))
                        patch.transform.localEulerAngles = -Vector3.up * 90f * Mathf.Sign(pos.y);
                    else
                        patch.transform.localEulerAngles = pos.x < 0f ? Vector3.up * 180f : Vector3.zero;
                }

                bool rotateXZOutwards = patchTypes[i] == PatchType.ExtraXZ || patchTypes[i] == PatchType.LessXZ || patchTypes[i] == PatchType.ExtraXLessZ || patchTypes[i] == PatchType.ExtraXZOuter;
                if (rotateXZOutwards)
                {
                    Vector3 from = new Vector3(1f, 0f, 1f).normalized;
                    Vector3 to = patch.transform.localPosition.normalized;
                    if (Mathf.Abs(patch.transform.localPosition.x) < 0.0001f || Mathf.Abs(Mathf.Abs(patch.transform.localPosition.x) - Mathf.Abs(patch.transform.localPosition.z)) > 0.001f)
                    {
                        Debug.LogWarning("Skipped rotating a patch because it isn't a corner, click here to highlight.", patch);
                        continue;
                    }
                    if (Vector3.Dot(from, to) < -0.99f)
                        patch.transform.localEulerAngles = Vector3.up * 180f;
                    else
                        patch.transform.localRotation = Quaternion.FromToRotation(from, to);
                }
            }
            return parent;
        }
    }
}

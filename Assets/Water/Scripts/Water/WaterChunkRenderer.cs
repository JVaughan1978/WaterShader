using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FEMA_AR.WATER
{
    public class WaterChunkRenderer : MonoBehaviour
    {
        public WaterRenderer waterRend;
        Bounds boundsLocal;
        Mesh mesh;
        Renderer rend;
        MaterialPropertyBlock mpb;
        int _lodIdx = -1;
        int _totalLODCount = -1;
        float _baseVertDensity = 32f;

        void Start()
        {
            rend = GetComponent<Renderer>();
            mesh = GetComponent<MeshFilter>().mesh;
            boundsLocal = mesh.bounds;
            mpb = new MaterialPropertyBlock();
        }

        //Called from WaterRenderer every frame
        public void UpdateMeshBounds(float maxHorizDisplacement, float maxVertDisplacement)
        {
            if (Mathf.Approximately(maxHorizDisplacement, 0) || Mathf.Approximately(maxVertDisplacement, 0))
                return;
            Bounds bounds = boundsLocal;
            float expandXZ = maxHorizDisplacement / transform.lossyScale.x;
            float boundsY = maxVertDisplacement / transform.lossyScale.y;
            bounds.extents = new Vector3(bounds.extents.x + expandXZ, boundsY, bounds.extents.z + expandXZ);
            mesh.bounds = bounds;
        }

        public void SetInstanceData(int lodIdx, int totalLODCount, float baseVertDensity)
        {
            _lodIdx = lodIdx;
            _totalLODCount = totalLODCount;
            _baseVertDensity = baseVertDensity;
        }

        void OnWillRenderObject()
        {
            if (mpb == null)
            {
                mpb = new MaterialPropertyBlock();
            }
            rend.GetPropertyBlock(mpb);

            float meshScaleLerp = 0f;
            float farNormalsWeight = 1f;
            if (_lodIdx == 0) meshScaleLerp = waterRend.viewerAltitudeLevel;
            if (_lodIdx == _totalLODCount - 1) farNormalsWeight = waterRend.viewerAltitudeLevel;
            mpb.SetVector("_InstanceData", new Vector4(meshScaleLerp, farNormalsWeight, _lodIdx, 0f));

            float squareSize = transform.lossyScale.x / _baseVertDensity;
            float mul = 1.875f;
            float pow = 1.4f;
            float normalScrollSpeed0 = Mathf.Pow(Mathf.Log(1f + 2f * squareSize) * mul, pow);
            float normalScrollSpeed1 = Mathf.Pow(Mathf.Log(1f + 4f * squareSize) * mul, pow);
            mpb.SetVector("_GeomData", new Vector4(squareSize, normalScrollSpeed0, normalScrollSpeed1, _baseVertDensity));

            waterRend.builder.waveCams[_lodIdx].ApplyMaterialBlockParams(0, mpb, true, true);
            if (_lodIdx + 1 < waterRend.builder.waveCams.Length)
            {
                waterRend.builder.waveCams[_lodIdx + 1].ApplyMaterialBlockParams(1, mpb, true, true);
            }
            rend.SetPropertyBlock(mpb);
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;

namespace FEMA_AR.WATER
{
    [RequireComponent(typeof(WaveSpectrum))]
    public class WaveBatched : MonoBehaviour
    {
        [Tooltip("Geometry to rasterize into wave buffers to generate waves.")]
        public Mesh rasterMesh;
        [Tooltip("Shader to be used to render out a single Gerstner octave.")]
        public Shader _waveShader;
        public int _randomSeed = 0;
        [HideInInspector] public WaterRenderer waterRenderer;

        const int BATCH_SIZE = 48;
        WaveSpectrum _spectrum;
        Material[] _materials;
        Material _materialBigWaveTransition;
        CommandBuffer[] cbWaveShapes;
        CommandBuffer cbLargeWaveLengths;
        CommandBuffer cbLargeWaveLengthTransition;

        static readonly float[] _wavelengthsBatch = new float[BATCH_SIZE];
        static readonly float[] _ampsBatch = new float[BATCH_SIZE];
        static readonly float[] _anglesBatch = new float[BATCH_SIZE];
        static readonly float[] _phasesBatch = new float[BATCH_SIZE];

        float[] _wavelengths;
        float[] _amplitudes;
        float[] _angleDegs;
        float[] _phases;
        bool _skip = false;

        void Start()
        {
            waterRenderer = FindObjectOfType<WaterRenderer>();
            _spectrum = GetComponent<WaveSpectrum>();
        }

        void Update()
        {
            Random.State randomStateBkp = Random.state;
            Random.InitState(_randomSeed);
            _spectrum.GenerateWaveData(ref _wavelengths, ref _angleDegs, ref _phases);
            Random.state = randomStateBkp;
            UpdateAmplitudes();
            ReportMaxDisplacement();

            if (_materials == null || _materials.Length != waterRenderer.builder.CurrentLodCount)
            {
                InitMaterials();
            }

            if (cbWaveShapes == null || cbWaveShapes.Length != waterRenderer.builder.CurrentLodCount - 1)
            {
                InitCommandBuffers();
            }
        }

        void UpdateAmplitudes()
        {
            if (_amplitudes == null || _amplitudes.Length != _wavelengths.Length)
            {
                _amplitudes = new float[_wavelengths.Length];
            }

            for (int i = 0; i < _wavelengths.Length; i++)
            {
                _amplitudes[i] = _spectrum.GetAmplitude(_wavelengths[i]);
            }
        }

        private void ReportMaxDisplacement()
        {
            float ampSum = 0f;
            for (int i = 0; i < _wavelengths.Length; i++)
            {
                ampSum += _amplitudes[i];
            }
            waterRenderer.GetMaxDisplacement(ampSum * _spectrum.chop, ampSum);
        }

        void InitMaterials()
        {
            foreach (var child in transform)
            {
                Destroy((child as Transform).gameObject);
            }
            _materials = new Material[waterRenderer.builder.CurrentLodCount];

            for (int i = 0; i < _materials.Length; i++)
            {
                _materials[i] = new Material(_waveShader);
            }

            _materialBigWaveTransition = new Material(_waveShader);
        }

        private void LateUpdate()
        {
            if(!_skip)
            {
            int componentIdx = 0;
            float minWl =
                waterRenderer.builder.waveCams[0].MaxWaveLength(
                    waterRenderer.XScale, waterRenderer.baseVertDensity, waterRenderer.minTexelsPerWave) / 2f;
            while (_wavelengths[componentIdx] < minWl && componentIdx < _wavelengths.Length)
            {
                componentIdx++;
            }
            RemoveDrawShapeCommandBuffers();

            for (int lod = 0; lod < waterRenderer.builder.CurrentLodCount - 1; lod++, minWl *= 2f)
            {
                int startCompIdx = componentIdx;
                while (componentIdx < _wavelengths.Length && _wavelengths[componentIdx] < 2f * minWl)
                {
                    componentIdx++;
                }

                if (UpdateBatch(lod, startCompIdx, componentIdx, _materials[lod]) > 0)
                {
                    AddDrawShapeCommandBuffer(lod);
                }
            }

            int lastBatchCount = UpdateBatch(waterRenderer.builder.CurrentLodCount - 1, componentIdx, _wavelengths.Length, _materials[waterRenderer.builder.CurrentLodCount - 1]);
            UpdateBatch(waterRenderer.builder.CurrentLodCount - 2, componentIdx, _wavelengths.Length, _materialBigWaveTransition);

            if (lastBatchCount > 0)
            {
                AddDrawShapeBigWavelengthsCommandBuffer();
            }
            }
            else
                _skip = false;
        }

        int UpdateBatch(int lodIdx, int firstComponent, int lastComponentNonInc, Material material)
        {
            int numComponents = lastComponentNonInc - firstComponent;
            int numInBatch = 0;
            int dropped = 0;

            // register any nonzero components
            for (int i = 0; i < numComponents; i++)
            {
                float wl = _wavelengths[firstComponent + i];
                float amp = _amplitudes[firstComponent + i];
                if (amp >= 0.001f)
                {
                    if (numInBatch < BATCH_SIZE)
                    {
                        _wavelengthsBatch[numInBatch] = wl;
                        _ampsBatch[numInBatch] = amp;
                        _anglesBatch[numInBatch] = Mathf.Deg2Rad * (waterRenderer.windDirectionAngle + _angleDegs[firstComponent + i]);
                        _phasesBatch[numInBatch] = _phases[firstComponent + i];
                        numInBatch++;
                    }
                    else
                    {
                        dropped++;
                    }
                }
            }

            if (dropped > 0)
            {
                Debug.LogWarning(string.Format("Gerstner LOD{0}: Batch limit reached, dropped {1} wavelengths. To support bigger batch sizes, see the comment around the BATCH_SIZE declaration.", lodIdx, dropped), this);
                numComponents = BATCH_SIZE;
            }

            if (numInBatch == 0)
            {
                return numInBatch;
            }

            if (numInBatch < BATCH_SIZE)
            {
                _wavelengthsBatch[numInBatch] = 0f;
            }
            material.SetFloat("_NumInBatch", numInBatch);
            material.SetFloat("_Chop", _spectrum.chop);
            material.SetFloatArray("_WaveLengths", _wavelengthsBatch);
            material.SetFloatArray("_Amplitudes", _ampsBatch);
            material.SetFloatArray("_Angles", _anglesBatch);
            material.SetFloatArray("_Phases", _phasesBatch);
            waterRenderer.builder.waveCams[lodIdx].ApplyMaterialParams(0, material, false, false);

            return numInBatch;
        }

        void AddDrawShapeCommandBuffer(int lodIndex)
        {
            waterRenderer.builder.shapeCams[lodIndex].AddCommandBuffer(CameraEvent.BeforeForwardAlpha, cbWaveShapes[lodIndex]);
        }

        void AddDrawShapeBigWavelengthsCommandBuffer()
        {
            int lastLod = waterRenderer.builder.CurrentLodCount - 1;
            waterRenderer.builder.shapeCams[lastLod].AddCommandBuffer(CameraEvent.BeforeForwardAlpha, cbLargeWaveLengths);
            waterRenderer.builder.shapeCams[lastLod - 1].AddCommandBuffer(CameraEvent.BeforeForwardAlpha, cbLargeWaveLengthTransition);
        }

        void RemoveDrawShapeCommandBuffers()
        {
            if (waterRenderer == null || waterRenderer.builder == null || cbLargeWaveLengths == null || cbLargeWaveLengthTransition == null)
            {
                return;
            }

            for (int lod = 0; lod < waterRenderer.builder.CurrentLodCount; lod++)
            {
                if (lod < waterRenderer.builder.CurrentLodCount - 1)
                {
                    if (cbWaveShapes == null || cbWaveShapes[lod] == null)
                    {
                        continue;
                    }
                    waterRenderer.builder.shapeCams[lod].RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, cbWaveShapes[lod]);
                }

                waterRenderer.builder.shapeCams[lod].RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, cbLargeWaveLengths);
                waterRenderer.builder.shapeCams[lod].RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, cbLargeWaveLengthTransition);
            }
        }

        void InitCommandBuffers()
        {
            Matrix4x4 drawMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(90f, Vector3.right), Vector3.one * 100000f);
            cbWaveShapes = new CommandBuffer[waterRenderer.builder.CurrentLodCount - 1];
            for (int i = 0; i < cbWaveShapes.Length; i++)
            {
                cbWaveShapes[i] = new CommandBuffer();
                cbWaveShapes[i].name = "ShapeGerstnerBatched" + i;
                cbWaveShapes[i].DrawMesh(rasterMesh, drawMatrix, _materials[i]);
            }

            cbLargeWaveLengths = new CommandBuffer();
            cbLargeWaveLengths.name = "ShapeGerstnerBatchedBigWavelengths";
            cbLargeWaveLengths.DrawMesh(rasterMesh, drawMatrix, _materials[waterRenderer.builder.CurrentLodCount - 1]);

            cbLargeWaveLengthTransition = new CommandBuffer();
            cbLargeWaveLengthTransition.name = "ShapeGerstnerBatchedBigWavelengthsTrans";
            cbLargeWaveLengthTransition.DrawMesh(rasterMesh, drawMatrix, _materialBigWaveTransition);
        }

        void OnEnable()
        {
            RemoveDrawShapeCommandBuffers();
            _skip = true;
        }

        void OnDisable()
        {
            RemoveDrawShapeCommandBuffers();
        }

        float ComputeWaveSpeed(float waveLength)
        {
            float g = 9.81f;
            float k = 2f * Mathf.PI / waveLength;
            float cp = Mathf.Sqrt(g / k);
            return cp;
        }

        public Vector3 GetPositionDisplacedToPositionExpensive(ref Vector3 displacedWorldPos, float timeOffset)
        {
            Vector3 worldDisp = displacedWorldPos;
            for (int i = 0; i < 4; i++)
            {
                Vector3 error = worldDisp + GetDisplacement(ref worldDisp, timeOffset) - displacedWorldPos;
                worldDisp.x -= error.x;
                worldDisp.z -= error.z;
            }
            worldDisp.y = waterRenderer.SeaLevel;
            return worldDisp;
        }

        public Vector3 GetDisplacement(ref Vector3 worldPos, float timeOffset)
        {
            if (_amplitudes == null)
            {
                return Vector3.zero;
            }

            Vector2 pos = new Vector2(worldPos.x, worldPos.z);
            float mytime = waterRenderer.ElapsedTime + timeOffset;
            float windAngle = waterRenderer.windDirectionAngle;
            Vector3 result = Vector3.zero;

            for (int j = 0; j < _amplitudes.Length; j++)
            {
                if (_amplitudes[j] <= 0.001f)
                {
                    continue;
                }

                float C = ComputeWaveSpeed(_wavelengths[j]);
                Vector2 D = new Vector2(Mathf.Cos((windAngle + _angleDegs[j]) * Mathf.Deg2Rad),
                                        Mathf.Sin((windAngle + _angleDegs[j]) * Mathf.Deg2Rad));
                float k = 2f * Mathf.PI / _wavelengths[j];
                float x = Vector2.Dot(D, pos);
                float t = k * (x + C * mytime) + _phases[j];
                float disp = -_spectrum.chop * Mathf.Sin(t);
                result += _amplitudes[j] * new Vector3(D.x * disp, Mathf.Cos(t), D.y * disp);
            }
            return result;
        }

        public Vector3 GetNormal(ref Vector3 worldPos, float timeOffset)
        {
            if (_amplitudes == null)
            {
                return Vector3.zero;
            }

            var pos = new Vector2(worldPos.x, worldPos.z);
            float mytime = waterRenderer.ElapsedTime + timeOffset;
            float windAngle = waterRenderer.windDirectionAngle;
            var delfdelx = Vector3.right;
            var delfdelz = Vector3.forward;

            for (int j = 0; j < _amplitudes.Length; j++)
            {
                if (_amplitudes[j] <= 0.001f)
                {
                    continue;
                }

                float C = ComputeWaveSpeed(_wavelengths[j]);
                var D = new Vector2(Mathf.Cos((windAngle + _angleDegs[j]) * Mathf.Deg2Rad),
                                    Mathf.Sin((windAngle + _angleDegs[j]) * Mathf.Deg2Rad));
                float k = 2f * Mathf.PI / _wavelengths[j];
                float x = Vector2.Dot(D, pos);
                float t = k * (x + C * mytime) + _phases[j];
                float disp = k * -_spectrum.chop * Mathf.Cos(t);
                float dispx = D.x * disp;
                float dispz = D.y * disp;
                float dispy = -k * Mathf.Sin(t);
                delfdelx += _amplitudes[j] * new Vector3(D.x * dispx, D.x * dispy, D.y * dispx);
                delfdelz += _amplitudes[j] * new Vector3(D.x * dispz, D.y * dispy, D.y * dispz);
            }
            return Vector3.Cross(delfdelz, delfdelx).normalized;
        }

        public float GetHeightExpensive(ref Vector3 worldPos, float timeOffset)
        {
            Vector3 posFlatland = worldPos;
            posFlatland.y = waterRenderer.transform.position.y;
            Vector3 undisplacedPos = GetPositionDisplacedToPositionExpensive(ref posFlatland, timeOffset);
            return posFlatland.y + GetDisplacement(ref undisplacedPos, timeOffset).y;
        }

        public Vector3 GetSurfaceVelocity(ref Vector3 worldPos, float timeOffset)
        {
            if (_amplitudes == null)
            {
                return Vector3.zero;
            }

            Vector2 pos = new Vector2(worldPos.x, worldPos.z);
            float mytime = waterRenderer.ElapsedTime + timeOffset;
            float windAngle = waterRenderer.windDirectionAngle;
            Vector3 result = Vector3.zero;

            for (int j = 0; j < _amplitudes.Length; j++)
            {
                if (_amplitudes[j] <= 0.001f)
                {
                    continue;
                }

                float C = ComputeWaveSpeed(_wavelengths[j]);
                Vector2 D = new Vector2(Mathf.Cos((windAngle + _angleDegs[j]) * Mathf.Deg2Rad),
                                        Mathf.Sin((windAngle + _angleDegs[j]) * Mathf.Deg2Rad));
                float k = 2f * Mathf.PI / _wavelengths[j];

                float x = Vector2.Dot(D, pos);
                float t = k * (x + C * mytime) + _phases[j];
                float disp = -_spectrum.chop * k * C * Mathf.Cos(t);
                result += _amplitudes[j] * new Vector3(D.x * disp, -k * C * Mathf.Sin(t), D.y * disp);
            }
            return result;
        }
    }
}
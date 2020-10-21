using UnityEngine;

namespace FEMA_AR.WATER
{
    public class WaveSpectrum : MonoBehaviour
    {
        [Range(0f, 1f)]
        public float weight = 1f;
        [Delayed]
        public int componentsPerOctave = 5;
        [Tooltip("Variance of flow direction, in degrees"), Range(0f, 180f)]
        public float waveDirectionVariance = 90f;
        [Tooltip("Scales horizontal displacement"), Range(0f, 2f)]
        public float chop = 1f;
        [HideInInspector] public WaterRenderer waterRenderer;
        [HideInInspector] public float[] powerLog = new float[NUM_OCTAVES];
        [HideInInspector] public bool[] powerDisabled = new bool[NUM_OCTAVES];
        [HideInInspector] public float windSpeed = 5f;
        [HideInInspector] public float fetch = 1000f;

        private const int NUM_OCTAVES = 12;
        public static readonly float SMALLEST_WL_POW_2 = -2f;
        public static readonly float MIN_POWER_LOG = -6f;
        public static readonly float MAX_POWER_LOG = 3f;
        public float SmallestWavelength { get { return Mathf.Pow(2f, SMALLEST_WL_POW_2); } }
        public float SmallWavelength(float octaveIndex) { return Mathf.Pow(2f, SMALLEST_WL_POW_2 + octaveIndex); }
        public float LargeWavelength(float octaveIndex) { return Mathf.Pow(2f, SMALLEST_WL_POW_2 + octaveIndex + 1f); }

        void Reset()
        {
            powerLog = new float[NUM_OCTAVES];

            for (int i = 0; i < powerLog.Length; i++)
            {
                powerLog[i] = MIN_POWER_LOG;
            }
        }

        float ComputeWaveSpeed(float wavelength)
        {
            float g = 9.81f;
            float k = 2f * Mathf.PI / wavelength;
            float cp = Mathf.Sqrt(g / k);
            return cp;
        }

        public float GetAmplitude(float wavelength)
        {
            if (wavelength <= 0.001f)
            {
                Debug.LogError("Wavelength must be >= 0f");
                return 0f;
            }

            float wl_pow2 = Mathf.Log(wavelength) / Mathf.Log(2f);
            wl_pow2 = Mathf.Clamp(wl_pow2, SMALLEST_WL_POW_2, SMALLEST_WL_POW_2 + NUM_OCTAVES - 1f);

            int index = (int)(wl_pow2 - SMALLEST_WL_POW_2);

            if (index >= powerLog.Length)
            {
                Debug.LogError("Out of bounds index");
                return 0f;
            }

            if (powerDisabled[index])
            {
                return 0f;
            }

            // The amplitude calculation follows this nice paper from Frechot:
            // https://hal.archives-ouvertes.fr/file/index/docid/307938/filename/frechot_realistic_simulation_of_ocean_surface_using_wave_spectra.pdf
            float wl_lo = Mathf.Pow(2f, Mathf.Floor(wl_pow2));
            float k_lo = 2f * Mathf.PI / wl_lo;
            float omega_lo = k_lo * ComputeWaveSpeed(wl_lo);
            float wl_hi = 2f * wl_lo;
            float k_hi = 2f * Mathf.PI / wl_hi;
            float omega_hi = k_hi * ComputeWaveSpeed(wl_hi);

            float domega = (omega_lo - omega_hi) / componentsPerOctave;

            float a_2 = 2f * Mathf.Pow(10f, powerLog[index]) * domega;
            var a = Mathf.Sqrt(a_2);
            return a * weight;
        }

        public void GenerateWaveData(ref float[] wavelengths, ref float[] anglesDeg, ref float[] phases)
        {
            int totalComponents = NUM_OCTAVES * componentsPerOctave;

            if (wavelengths == null || wavelengths.Length != totalComponents) wavelengths = new float[totalComponents];
            if (anglesDeg == null || anglesDeg.Length != totalComponents) anglesDeg = new float[totalComponents];
            if (phases == null || phases.Length != totalComponents) phases = new float[totalComponents];

            float minWavelength = Mathf.Pow(2f, SMALLEST_WL_POW_2);

            for (int octave = 0; octave < NUM_OCTAVES; octave++)
            {
                for (int i = 0; i < componentsPerOctave; i++)
                {
                    int index = octave * componentsPerOctave + i;
                    wavelengths[index] = minWavelength * (1f + Random.value);
                    anglesDeg[index] = Random.Range(-waveDirectionVariance, waveDirectionVariance);
                    phases[index] = 2f * Mathf.PI * Random.value;
                }

                System.Array.Sort(wavelengths, octave * componentsPerOctave, componentsPerOctave);

                minWavelength *= 2f;
            }
        }

        [System.NonSerialized] public bool _applyPhillipsSpectrum = false;
        [System.NonSerialized] public bool _applyPiersonMoskowitzSpectrum = false;
        [System.NonSerialized] public bool _applyJONSWAPSpectrum = false;

        public void ApplyPhillipsSpectrum(float windSpeed)
        {
            if (waterRenderer == null)
            {
                waterRenderer = FindObjectOfType<WaterRenderer>();
            }

#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Apply Phillips Spectrum");
#endif

            for (int octave = 0; octave < NUM_OCTAVES; octave++)
            {
                float wl = SmallWavelength(octave) * 1.5f;
                var pow = PhillipsSpectrum(windSpeed, waterRenderer.WindDir, Mathf.Abs(Physics.gravity.y), Mathf.Pow(2f, SMALLEST_WL_POW_2), wl, 0f);
                pow = Mathf.Max(pow, Mathf.Pow(10f, MIN_POWER_LOG));
                powerLog[octave] = Mathf.Log10(pow);
            }
        }

        public void ApplyPiersonMoskowitzSpectrum(float windSpeed)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Apply Pierson-Moskowitz Spectrum");
#endif

            for (int octave = 0; octave < NUM_OCTAVES; octave++)
            {
                float wl = SmallWavelength(octave) * 1.5f;
                var pow = PiersonMoskowitzSpectrum(Mathf.Abs(Physics.gravity.y), windSpeed, wl);
                pow = Mathf.Max(pow, Mathf.Pow(10f, MIN_POWER_LOG));
                powerLog[octave] = Mathf.Log10(pow);
            }
        }

        public void ApplyJONSWAPSpectrum(float windSpeed)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Apply JONSWAP Spectrum");
#endif
            for (int octave = 0; octave < NUM_OCTAVES; octave++)
            {
                float wl = SmallWavelength(octave) * 1.5f;
                var pow = JONSWAPSpectrum(Mathf.Abs(Physics.gravity.y), windSpeed, wl, fetch);
                pow = Mathf.Max(pow, Mathf.Pow(10f, MIN_POWER_LOG));
                powerLog[octave] = Mathf.Log10(pow);
            }
        }

        static float PhillipsSpectrum(float windSpeed, Vector2 windDir, float gravity, float smallestWavelength, float wavelength, float angle)
        {
            float wavenumber = 2f * Mathf.PI / wavelength;
            float angle_radians = Mathf.PI * angle / 180f;
            float kx = Mathf.Cos(angle_radians) * wavenumber;
            float kz = Mathf.Sin(angle_radians) * wavenumber;
            float k2 = kx * kx + kz * kz;
            float windSpeed2 = windSpeed * windSpeed;
            float wx = windDir.x;
            float wz = windDir.y;
            float kdotw = (wx * kx + wz * kz);
            float a = 0.0081f;
            float L = windSpeed2 / gravity;
            return a * kdotw * kdotw * Mathf.Exp(-1f / (k2 * L * L)) / (k2 * k2);
        }

        static float PhilSpectrum(float gravity, float wavelength)
        {
            float alpha = 0.0081f;
            return PhilSpectrum(gravity, alpha, wavelength);
        }

        static float PhilSpectrum(float gravity, float alpha, float wavelength)
        {
            float wavenumber = 2f * Mathf.PI / wavelength;
            float frequency = Mathf.Sqrt(gravity * wavenumber);
            return alpha * gravity * gravity / Mathf.Pow(frequency, 5f);
        }

        static float PiersonMoskowitzSpectrum(float gravity, float windspeed, float wavelength)
        {
            float wavenumber = 2f * Mathf.PI / wavelength;
            float frequency = Mathf.Sqrt(gravity * wavenumber); // deep water - depth > wavelength/2
            float frequency_peak = 0.855f * gravity / windspeed;
            return PhilSpectrum(gravity, wavelength) * Mathf.Exp(-Mathf.Pow(frequency_peak / frequency, 4f) * 5f / 4f);
        }

        static float PiersonMoskowitzSpectrum(float gravity, float windspeed, float frequency_peak, float alpha, float wavelength)
        {
            float wavenumber = 2f * Mathf.PI / wavelength;
            float frequency = Mathf.Sqrt(gravity * wavenumber); // deep water - depth > wavelength/2
            return PhilSpectrum(gravity, alpha, wavelength) * Mathf.Exp(-Mathf.Pow(frequency_peak / frequency, 4f) * 5f / 4f);
        }

        static float JONSWAPSpectrum(float gravity, float windspeed, float wavelength, float fetch)
        {
            float F = fetch;
            float alpha = 0.076f * Mathf.Pow(windspeed * windspeed / (F * gravity), 0.22f);
            float wavenumber = 2f * Mathf.PI / wavelength;
            float frequency = Mathf.Sqrt(gravity * wavenumber);
            float frequency_peak = 22f * Mathf.Pow(gravity * gravity / (windspeed * F), 1f / 3f);
            float sigma = frequency <= frequency_peak ? 0.07f : 0.09f;
            float r = Mathf.Exp(-Mathf.Pow(frequency - frequency_peak, 2f) / (2f * sigma * sigma * frequency_peak * frequency_peak));
            float gamma = 3.3f;
            return PiersonMoskowitzSpectrum(gravity, windspeed, frequency_peak, alpha, wavelength) * Mathf.Pow(gamma, r);
        }
    }
}

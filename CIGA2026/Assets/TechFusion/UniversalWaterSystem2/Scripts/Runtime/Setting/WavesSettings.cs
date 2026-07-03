using UnityEngine;

namespace UniversalWaterSystem
{
    public struct SpectrumSettings
    {
        public float scale;
        public float angle;
        public float spread;
        public float swell;
        public float alpha;
        public float peakOmega;
        public float gamma;
        public float waveLengthFilter;
    }

    [System.Serializable]
    public struct DisplaySpectrumSettings
    {
        [Range(0, 1)]
        public float scale;
        [Range(0.01f, 50)]
        public float windScale;
        [Range(0, 360)]
        public float windDirection;
        [Range(1, 2000000)]
        public float tileStrength;
        [Range(0, 1)]
        public float spread;
        [Range(0, 1)]
        public float swell;
        [Range(0.1f, 5)]
        public float peak;
        [Range(0, 10)]
        public float waveLengthFilter;
    }

    [CreateAssetMenu(fileName = "New waves settings", menuName = "UniversalWaterSystem/Waves Settings")]
    public class WavesSettings : ScriptableObject
    {
        public float g;
        public float depth;
        [Range(0, 2)]
        public float lambda = 1;
        //[Range(0.1f, 150)]
        //public float windSpeed = 1;
        public DisplaySpectrumSettings local;
        public DisplaySpectrumSettings swell;

        SpectrumSettings[] spectrums = new SpectrumSettings[2];

        public void SetParametersToShader(ComputeShader shader, int kernelIndex, ComputeBuffer paramsBuffer)
        {
            shader.SetFloat(G_PROP, g);
            shader.SetFloat(DEPTH_PROP, depth);

            FillSettingsStruct(local, ref spectrums[0]);
            FillSettingsStruct(swell, ref spectrums[1]);

            paramsBuffer.SetData(spectrums);
            shader.SetBuffer(kernelIndex, SPECTRUMS_PROP, paramsBuffer);
        }

        void FillSettingsStruct(DisplaySpectrumSettings display, ref SpectrumSettings settings)
        {
            settings.scale = display.scale;
            settings.angle = display.windDirection / 180 * Mathf.PI;
            settings.spread = display.spread;
            settings.swell = Mathf.Clamp(display.swell, 0.01f, 1);
            settings.alpha = JonswapAlpha(g, display.tileStrength, display.windScale);
            settings.peakOmega = JonswapPeakFrequency(g, display.tileStrength, display.windScale);
            settings.gamma = display.peak;
            settings.waveLengthFilter = display.waveLengthFilter;
        }

        float JonswapAlpha(float g, float tileStrength, float windSpeed)
        {
            return 0.076f * Mathf.Pow(g * tileStrength / windSpeed / windSpeed, -0.22f);
        }

        float JonswapPeakFrequency(float g, float tileStrength, float windSpeed)
        {
            return 22 * Mathf.Pow(windSpeed * tileStrength / g / g, -0.33f);
        }

        readonly int G_PROP = Shader.PropertyToID("GravityAcceleration");
        readonly int DEPTH_PROP = Shader.PropertyToID("Depth");
        readonly int SPECTRUMS_PROP = Shader.PropertyToID("Spectrums");
    }
}
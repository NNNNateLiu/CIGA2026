using UnityEngine;

namespace UniversalWaterSystem
{
    public static class GlobalShaderVariables
    {
        public static class Misc
        {
            public static readonly int InverseViewMatrix = Shader.PropertyToID("Water_InverseViewMatrix");
            public static readonly int InverseProjectionMatrix = Shader.PropertyToID("Water_InverseProjectionMatrix");
            public static readonly int SubmergenceTexture = Shader.PropertyToID("Water_CameraSubmergenceTexture");
        }

        public static class Simulation
        {

            //shoreline
            public static readonly int ShorelineWaveVariable = Shader.PropertyToID("WaveVariable");
            public static readonly int ShorelineVariationVariable = Shader.PropertyToID("VariationVariable");
            public static readonly int ShorelineFoamSpaceVariable = Shader.PropertyToID("FoamSpaceVariable");
            public static readonly int ShorelineFoamTimeVariable = Shader.PropertyToID("FoamTimeVariable");
            public static readonly int ShorelineCurvePoints = Shader.PropertyToID("CurvePoints");
            public static readonly int ShorelineCurvePointCount = Shader.PropertyToID("CurvePointCount");
            public static readonly int ShorelineWidth = Shader.PropertyToID("ShorelineWidth");
            public static readonly int ShorelineWaveFade = Shader.PropertyToID("WaveFade");

            //cascade wave
            public static readonly int CascadeDisplacement = Shader.PropertyToID("Water_CascadeDisplacement");
            public static readonly int CascadeFoam = Shader.PropertyToID("Water_CascadeFoam");
            public static readonly int CascadeFoamCurrent = Shader.PropertyToID("Water_CascadeFoamCurrent");
            public static readonly int CascadeFoamHistory = Shader.PropertyToID("Water_CascadeFoamHistory");
            public static readonly int CascadePosScale = Shader.PropertyToID("Water_CascadePosScale");
            public static readonly int CascadeSize = Shader.PropertyToID("Water_CascadeSize");
            public static readonly int PrevCascadePosScale = Shader.PropertyToID("Water_PrevCascadePosScale");
            public static readonly int PrevCascadeSize = Shader.PropertyToID("Water_PrevCascadeSize");
            public static readonly int CascadeIndex = Shader.PropertyToID("Water_CascadeIndex");
        }
    }
}

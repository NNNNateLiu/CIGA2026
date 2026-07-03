using UnityEngine;

namespace UniversalWaterSystem
{
    [CreateAssetMenu(fileName = "New UWS Colors", menuName = "UniversalWaterSystem/Colors Preset")]
    public class ColorsPreset : ScriptableObject
    {
        public Gradient _absorptionRamp;
        public Gradient _scatterRamp;
    }
}
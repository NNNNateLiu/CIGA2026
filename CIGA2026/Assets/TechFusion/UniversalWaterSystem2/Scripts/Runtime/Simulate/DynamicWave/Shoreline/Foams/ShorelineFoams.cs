using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace UniversalWaterSystem
{
    [RequireComponent(typeof(VisualEffect))]
    public class ShorelineFoams : MonoBehaviour
    {
        public float FoamStrength;
        public float WaveFade;
        public Vector4 FoamTimeVariable;

        public ShorelineWaveGenerator waveGenerator;

        public ShorelineFoamPointsBuilder positionBuilder;
        private VisualEffect foamVFX;

        private void Awake()
        {
            foamVFX = GetComponent<VisualEffect>();
            positionBuilder.foams = this;
            positionBuilder.Resize(64, 64);
        }

        // Start is called before the first frame update
        void Start()
        {
        
        }

        private void OnDestroy()
        {
            if (positionBuilder != null)
            {
                positionBuilder.Dispose();
            }
        }

        public void SetVisible(bool v)
        {
            if (foamVFX != null)
            {
                foamVFX.enabled = v;
            }
        }

        public void AddData(List<Vector3> newData, float waveLinearPos)
        {
            if (positionBuilder != null)
            {
                positionBuilder.AddData(newData, waveLinearPos);
            }
        }

        // Update is called once per frame
        public void BuildData()
        {
            if (positionBuilder != null)
            {
                positionBuilder.BuildData();

                foamVFX.SetUInt("ValidDataCount", (uint)positionBuilder.GetValidDataCount());
                foamVFX.SetTexture("PositionMap", positionBuilder.GetDataMap());

                foamVFX.SetFloat("FoamStrength", FoamStrength);
                foamVFX.SetFloat("WaveFade", WaveFade);
                foamVFX.SetVector4("FoamTimeVariable", FoamTimeVariable);
            }
        }
    }
}

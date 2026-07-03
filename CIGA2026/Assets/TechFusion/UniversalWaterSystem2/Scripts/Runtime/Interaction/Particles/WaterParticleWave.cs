using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalWaterSystem
{
    public class WaterParticleWave : MonoBehaviour
    {
        private void OnEnable()
        {
            ParticleSystem particleSystem = GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                Water.Instance.AddWaveParticle(particleSystem);
            }
        }

        private void OnDisable()
        {
            ParticleSystem particleSystem = GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                Water.Instance.RemoveWaveParticle(particleSystem);
            }
        }
    }
}

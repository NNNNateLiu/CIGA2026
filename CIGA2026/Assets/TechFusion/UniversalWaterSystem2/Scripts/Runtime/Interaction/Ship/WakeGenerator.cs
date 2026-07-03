using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalWaterSystem
{
    public class WakeGenerator : MonoBehaviour
    {
        public ParticleSystem frontWave;
        public ParticleSystem rearWave;
        public float waveVelocityToEmissionRate = 1;

        public ParticleSystem frontFoam;
        public ParticleSystem rearFoam;
        public float foamVelocityToEmissionRate = 1;

        public float underwaterFadeDistance = 20;

        private Rigidbody rigidBody;

        private void Awake()
        {
            rigidBody = GetComponent<Rigidbody>();
        }

        // Start is called before the first frame update
        void Start()
        {
            if (frontWave != null)
                Water.Instance.AddWaveParticle(frontWave);

            if (rearWave != null)
                Water.Instance.AddWaveParticle(rearWave);
        }

        private void OnDestroy()
        {
            if (frontWave != null)
                Water.Instance.RemoveWaveParticle(frontWave);

            if (rearWave != null)
                Water.Instance.RemoveWaveParticle(rearWave);
        }

        // Update is called once per frame
        void Update()
        {
            UpdateWakeParticles();
        }

        float GetSailWaveSpeed()
        {
            Vector3 velocity = rigidBody.velocity;
            Vector3 horizonVel = velocity;
            horizonVel.y = 0;

            Vector3 shipDirection = transform.forward;
            Vector3 horizonDirection = shipDirection;
            horizonDirection.y = 0;
            horizonDirection.Normalize();

            float sailSpeed = Mathf.Max(Vector3.Dot(horizonVel, horizonDirection), 0);
            return sailSpeed;
        }

        void UpdateWakeParticles()
        {
            float sailSpeed = GetSailWaveSpeed();

            if (frontWave != null)
            {
                float waveEmissionRate = sailSpeed * waveVelocityToEmissionRate;
                var waveEmissionModule = frontWave.emission;
                waveEmissionModule.rateOverTime = waveEmissionRate;
                var waveRenderer = frontWave.GetComponent<ParticleSystemRenderer>();
                waveRenderer.material.color = Color.white * CalculateFade(frontWave.transform.position);
            }

            if (rearWave != null)
            {
                float waveEmissionRate = sailSpeed * waveVelocityToEmissionRate * 1.7f;
                var waveEmissionModule = rearWave.emission;
                waveEmissionModule.rateOverTime = waveEmissionRate;
                var waveRenderer = rearWave.GetComponent<ParticleSystemRenderer>();
                waveRenderer.material.color = Color.white * CalculateFade(rearWave.transform.position) * 0.6f;
            }

            if (frontFoam != null)
            {
                float foamEmissionRate = sailSpeed * foamVelocityToEmissionRate;
                var foamEmissionModule = frontFoam.emission;
                foamEmissionModule.rateOverTime = foamEmissionRate * CalculateFade(frontFoam.transform.position);
            }
        }

        float CalculateFade(Vector3 pos)
        {
            float waterHeight = Water.Instance.GetWaterHeight(pos);
            float factor = Mathf.Clamp01((waterHeight - pos.y) / underwaterFadeDistance);
            return Mathf.Lerp(1.0f, 0.0f, factor);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UniversalWaterSystem
{
    public partial class Water : MonoBehaviour
    {
        //area transform
        public float dynamicScale = 20;

        //[Range(1, 16)]
        public int cascadeCount = 3;

        public enum CascadeResolutionLevel
        {
            RES_256x256,
            RES_512x512,
            RES_1024x1024,
        }

        [SerializeField]
        public CascadeResolutionLevel cascadeResolution = CascadeResolutionLevel.RES_512x512;

        public int CascadeResolution
        {
            get
            {
                switch (cascadeResolution)
                {
                    case CascadeResolutionLevel.RES_256x256:
                        return 256;
                    case CascadeResolutionLevel.RES_512x512:
                        return 512;
                    case CascadeResolutionLevel.RES_1024x1024:
                        return 1024;
                }
                return 512;
            }
        }
        public float Scale { get { return dynamicScale; } set { dynamicScale = value; } }
        public float CalcLodScale(float lodIndex) { return Scale * Mathf.Pow(2f, lodIndex); }
        public float CalcGridSize(int lodIndex) { return CalcLodScale(lodIndex) / CascadeResolution; }

        //public static OceanSimulation Instance { get; private set; }

        [HideInInspector] public CascadeTransform cascadeTransform;
        private DynamicWaves waveMgr;

        private List<ParticleSystem> waveParticles = new List<ParticleSystem>();

        public List<ShorelineWaveGenerator> shorelineGenerators = new List<ShorelineWaveGenerator>();

        public void InitDynamicWaves()
        {
            if (cascadeTransform == null)
            {
                cascadeTransform = new CascadeTransform();
                cascadeTransform.InitCascadeData(cascadeCount);
            }

            if (waveMgr == null)
            {
                waveMgr = new DynamicWaves();
                waveMgr.Init(cascadeCount);
            }

        }

        public void UpdateDynamicWaves()
        {
            CommandBuffer cmd = CommandBufferPool.Get("Water Dynamic Simulation");

            cascadeTransform?.UpdateTransforms();
            waveMgr?.UpdateData();
            waveMgr?.SetGlobalShaderVariables();
            waveMgr?.BuildCommandBuffer(cmd);


            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public List<ParticleSystem> GetWaveParticles()
        {
            return waveParticles;
        }

        public void AddWaveParticle(ParticleSystem particle)
        {
            waveParticles.Add(particle);
        }

        public void RemoveWaveParticle(ParticleSystem particle) 
        {  
            waveParticles.Remove(particle);
        }

        public void RegisterShorelineGenerator(ShorelineWaveGenerator shoreline)
        {
            if (!shorelineGenerators.Contains(shoreline))
                shorelineGenerators.Add(shoreline);
        }

        public void UnRegisterShorelineGenerator(ShorelineWaveGenerator shoreline)
        {
            if (shorelineGenerators.Contains(shoreline))
                shorelineGenerators.Remove(shoreline);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalWaterSystem
{
    [ExecuteAlways]
    public partial class Water : MonoBehaviour
    {
        static public Water Instance { get; private set; }

        float waterTime = 0;

        // Start is called before the first frame update
        void Awake()
        {
            Instance = this;
            waterTime = 0;

            InitLUT();
            InitFFTWaves();
            InitDynamicWaves();
            InitResources();
            InitGeometry();
        }

        private void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            waterTime += Time.deltaTime;

            UpdateFFTWaves();
            UpdateDynamicWaves();
            UpdateGeometry();
        }
    }
}
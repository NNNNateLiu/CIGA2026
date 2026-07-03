using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalWaterSystem
{
    public class SplashPlayer : MonoBehaviour
    {
        public static SplashPlayer Instance;
        public GameObject splashPrefab;
        public GameObject ballPrefab;
        public GameObject floatObjectPrefab;
        public float spawnDuration = 2.0f;
        public Transform spawnCenter;
        public float spawnRadius = 20.0f;

        private void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            SpawnFloatObjects();

            StartCoroutine(SpawnSplashBall());
        }

        void SpawnFloatObjects()
        {
            int floatNum = 20;
            for (int i = 0; i < 20;  i++)
            {
                Vector3 spawnCenterPos = spawnCenter ? spawnCenter.position : Vector3.zero;
                float rot = i * 360.0f / floatNum;
                Vector3 floatPosition = spawnCenterPos + Quaternion.Euler(0, rot, 0) * Vector3.right * spawnRadius;
                GameObject floatObject = Instantiate(floatObjectPrefab, floatPosition, Quaternion.identity);
                floatObject.transform.localScale = new Vector3(0.5f, 5, 1);
            }
        }

        IEnumerator SpawnSplashBall()
        {
            while (true)
            {
                yield return new WaitForSeconds(spawnDuration);

                Vector3 spawnCenterPos = spawnCenter ? spawnCenter.position : Vector3.zero;
                float r = Random.Range(0.1f, spawnRadius);
                Vector3 spawnPosition = spawnCenterPos + Quaternion.Euler(0, Random.Range(0, 360.0f), 0) * Vector3.right * r;
                GameObject ball = Instantiate(ballPrefab, spawnPosition, Quaternion.identity);
            }
        }

        public void SpawnSplash(Vector3 pos)
        {
            GameObject splash = Instantiate(splashPrefab, pos, Quaternion.identity);
            Destroy(splash, 3);
        }
    }
}


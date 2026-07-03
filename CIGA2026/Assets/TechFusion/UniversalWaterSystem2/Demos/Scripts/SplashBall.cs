using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalWaterSystem
{
    public class SplashBall : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {

        }

        private void FixedUpdate()
        {
            float waterHeight = Water.Instance.GetWaterHeight(transform.position);

            if (waterHeight > transform.position.y)
            {
                SplashPlayer.Instance.SpawnSplash(transform.position);

                Destroy(gameObject);
            }
        }
    }
}


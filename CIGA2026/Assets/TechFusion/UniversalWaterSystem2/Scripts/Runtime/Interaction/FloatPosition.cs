using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalWaterSystem
{
    public class FloatPosition : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
            float waterLevelHeight = Water.Instance.GetWaterHeight(transform.position);
            transform.position = new Vector3(transform.position.x, waterLevelHeight, transform.position.z);
        }
    }
}

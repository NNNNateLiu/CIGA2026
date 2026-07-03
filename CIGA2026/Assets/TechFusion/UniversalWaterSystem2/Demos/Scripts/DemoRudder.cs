using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalWaterSystem
{
    public class DemoRudder : MonoBehaviour
    {
        public enum AxisEnum
        {
            XAxis = 0,
            YAxis,
            ZAxis,
        }

        public Transform _rudder;
        public AxisEnum axis = AxisEnum.ZAxis;
        [Range(0, 180)]
        public float _rotRange = 170;
        [Range(1, 500)]
        public float _rotSpeed = 10;
        float _currentRot = 0;
        // Start is called before the first frame update
        void Start()
        {

        }

        public void AddRot(float delta)
        {
            _currentRot += delta * _rotSpeed;
            _currentRot = Mathf.Clamp(_currentRot, -_rotRange, _rotRange);
        }

        // Update is called once per frame
        void Update()
        {
            if (_rudder != null)
            {
                if (axis == AxisEnum.XAxis)
                {
                    _rudder.localRotation = Quaternion.Euler(_currentRot, 0, 0);
                }
                else if (axis == AxisEnum.YAxis)
                {
                    _rudder.localRotation = Quaternion.Euler(0, _currentRot, 0);
                }
                else
                {
                    _rudder.localRotation = Quaternion.Euler(0, 0, _currentRot);
                }
            }
        }
    }
}


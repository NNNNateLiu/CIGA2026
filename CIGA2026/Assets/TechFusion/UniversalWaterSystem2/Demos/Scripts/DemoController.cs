using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalWaterSystem
{ 
    public class DemoController : MonoBehaviour
    {
        public DemoCameraController _cameraController;

        public ShipDynamics _boat;

        public DemoRudder _rudder;

        public GameObject _joystick;
        // Start is called before the first frame update
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                SwitchWatch();
            }

            if (_joystick)
            {
                SetBoatImpetus(_joystick.GetComponent<MobileInputController>().Vertical, _joystick.GetComponent<MobileInputController>().Horizontal);
                RotRudder(_joystick.GetComponent<MobileInputController>().Horizontal);
            }
            else
            {
                SetBoatImpetus(Input.GetAxisRaw("Vertical"), Input.GetAxisRaw("Horizontal"));
                RotRudder(Input.GetAxisRaw("Horizontal"));
            }
        }

        public void SwitchWatch()
        {
            if (_cameraController != null)
            {
                _cameraController.SwitchWatch();
            }
        }

        public void SetBoatImpetus(float vertical, float horizontal)
        {
            if (_boat)
            {
                _boat.SetImpetus(vertical, horizontal);
            }
        }

        public void RotRudder(float horizontal)
        {
            if (_rudder)
            {
                _rudder.AddRot(-horizontal * Time.deltaTime);
            }
        }
    }
}
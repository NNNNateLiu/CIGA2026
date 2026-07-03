using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalWaterSystem
{
    public class DemoCameraController : MonoBehaviour
    {
        public Camera _camera;

        public Transform[] _watchPoints;

        private List<Transform> _watchCircle = new List<Transform>();

        private int _watchIndex = 0;

        //private Plane waterPlane;

        // Start is called before the first frame update
        void Start()
        {
            //waterPlane = new Plane(Vector3.up, 0);

            if (Water.Instance)
            {
                Water.Instance.SetViewer(_camera.transform);
            }

            foreach (Transform t in _watchPoints)
            {
                _watchCircle.Add(t);
            }

            UpdateCameraWatch();
        }

        void UpdateCameraWatch()
        {
            ////attach
            //if (_watchIndex == 0)
            //{
            //    if (_attachTransform != null && _camera.transform.parent != _attachTransform)
            //    {
            //        _camera.transform.SetParent(_attachTransform);
            //        _camera.transform.localPosition = Vector3.zero;
            //        _camera.transform.localRotation = Quaternion.identity;
            //    }
            //}
            //else //watch
            {
                Transform t = _watchCircle[_watchIndex];
                if (t != null)
                {
                    //_camera.transform.SetParent(null);
                    //_camera.transform.position = t.position;
                    //_camera.transform.rotation = t.rotation;
                    _camera.transform.SetParent(t);
                    _camera.transform.localPosition = Vector3.zero;
                    _camera.transform.localRotation = Quaternion.identity;
                }
            }
        }

        public void SwitchWatch()
        {
            _watchIndex++;
            if (_watchIndex >= _watchCircle.Count)
            {
                _watchIndex = 0;
            }

            UpdateCameraWatch();
        }

        //private void Update()
        //{
        //    Vector3 lookPosition; = waterPlane.Raycast
        //}
    }
}


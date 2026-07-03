using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UniversalWaterSystem
{
    public class CascadeTransform
    {
        [System.Serializable]
        public struct RenderData
        {
            public float _texelWidth;
            public float _textureRes;
            public Vector3 _posSnapped;

            public Rect RectXZ
            {
                get
                {
                    float w = _texelWidth * _textureRes;
                    return new Rect(_posSnapped.x - w / 2f, _posSnapped.z - w / 2f, w, w);
                }
            }
        }

        public RenderData[] _renderData = null;
        public RenderData[] _renderDataSource = null;

        public int CascadeCount { get; private set; }

        Matrix4x4[] _worldToCameraMatrix;
        Matrix4x4[] _projectionMatrix;
        public Matrix4x4 GetWorldToCameraMatrix(int lodIdx) { return _worldToCameraMatrix[lodIdx]; }
        public Matrix4x4 GetProjectionMatrix(int lodIdx) { return _projectionMatrix[lodIdx]; }

        public void InitCascadeData(int count)
        {
            CascadeCount = count;

            _renderData = new RenderData[count];
            _renderDataSource = new RenderData[count];
            _worldToCameraMatrix = new Matrix4x4[count];
            _projectionMatrix = new Matrix4x4[count];
        }

        public void UpdateTransforms()
        {
            Transform viewerTransform = Water.Instance.GetViewer();
            if (viewerTransform == null)
            {
                Debug.Log("Set a Viewer for Water!");
                viewerTransform = Water.Instance.transform;
            }

            for (int lodIdx = 0; lodIdx < CascadeCount; lodIdx++)
            {
                _renderDataSource[lodIdx] = _renderData[lodIdx];

                var lodScale = Water.Instance.CalcLodScale(lodIdx);
                var camOrthSize = 2f * lodScale;

                // find snap period
                _renderData[lodIdx]._textureRes = Water.Instance.CascadeResolution;
                _renderData[lodIdx]._texelWidth = 2f * camOrthSize / _renderData[lodIdx]._textureRes;
                // snap so that shape texels are stationary
                _renderData[lodIdx]._posSnapped = viewerTransform.position
                    - new Vector3(Mathf.Repeat(viewerTransform.position.x, _renderData[lodIdx]._texelWidth), 0f, Mathf.Repeat(viewerTransform.position.z, _renderData[lodIdx]._texelWidth));

                // detect first update and populate the render data if so - otherwise it can give divide by 0s and other nastiness
                if (_renderDataSource[lodIdx]._textureRes == 0f)
                {
                    _renderDataSource[lodIdx]._posSnapped = _renderData[lodIdx]._posSnapped;
                    _renderDataSource[lodIdx]._texelWidth = _renderData[lodIdx]._texelWidth;
                    _renderDataSource[lodIdx]._textureRes = _renderData[lodIdx]._textureRes;
                }

                _worldToCameraMatrix[lodIdx] = CalculateWorldToCameraMatrixRHS(_renderData[lodIdx]._posSnapped + Vector3.up * 100f, Quaternion.AngleAxis(90f, Vector3.right));

                _projectionMatrix[lodIdx] = Matrix4x4.Ortho(-camOrthSize, camOrthSize, -camOrthSize, camOrthSize, 1f, 1000f);
            }
        }

        // Borrowed from SRP code: https://github.com/Unity-Technologies/ScriptableRenderPipeline/blob/2a68d8073c4eeef7af3be9e4811327a522434d5f/com.unity.render-pipelines.high-definition/Runtime/Core/Utilities/GeometryUtils.cs
        public static Matrix4x4 CalculateWorldToCameraMatrixRHS(Vector3 position, Quaternion rotation)
        {
            return Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
        }

        public void SetViewProjectionMatrices(int lodIdx, CommandBuffer cmd)
        {
            cmd.SetViewProjectionMatrices(GetWorldToCameraMatrix(lodIdx), GetProjectionMatrix(lodIdx));
        }
    }
}

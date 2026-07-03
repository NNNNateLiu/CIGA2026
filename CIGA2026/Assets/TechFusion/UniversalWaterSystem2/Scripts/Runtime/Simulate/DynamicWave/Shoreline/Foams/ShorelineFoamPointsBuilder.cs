using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalWaterSystem
{
    struct ShorelineFoamPointsData
    {
        public Vector3 position;
        public Vector2 fadeParams;
    }

    //The position information required for generating Foam VFX.
    [System.Serializable]
    public class ShorelineFoamPointsBuilder : IDisposable
    {
        const int PointDataPropertiesCount = 5; //position, fadeParams

        public ShorelineFoams foams;

        [SerializeField] protected ComputeShader baker;

        int mapWidth;
        int mapHeight;
        int validDataCount = 0;
        ShorelineFoamPointsData[] pointsData;

        ComputeBuffer dataBuffer;
        RenderTexture dataMap;
        RenderTexture tempDataMap;

        public RenderTexture GetDataMap()
        {
            return dataMap;
        }

        public int GetValidDataCount()
        {
            return validDataCount;
        }

        public void ResetValidDataCount()
        {
            validDataCount = 0;
        }

        public void Resize(int width, int height)
        {
            if(width != mapWidth || height != mapHeight)
            {
                Dispose();
            }

            int count = width * height;
            pointsData = new ShorelineFoamPointsData[count];
            for (int i = 0; i < count; i++)
            {
                pointsData[i] = new ShorelineFoamPointsData();
            }

            validDataCount = 0;

            mapWidth = width;
            mapHeight = height;
        }

        public void AddData(List<Vector3> newData, float waveLinearPos)
        {
            //TODO
            if (waveLinearPos > 0.97) return;

            if (pointsData != null)
            {
                for (int i = 0; i < newData.Count; i++)
                {
                    if (validDataCount >= pointsData.Length) break;

                    pointsData[validDataCount].position = newData[i];

                    pointsData[validDataCount].fadeParams.x = foams.waveGenerator.displacementEdgeFade.Evaluate((float)i / newData.Count);
                    pointsData[validDataCount].fadeParams.y = foams.waveGenerator.displacementWaveFade.Evaluate(waveLinearPos);
                    
                    validDataCount++;
                }
            }
        }

        public void BuildData()
        {
            if (pointsData != null && baker != null)
            {
                BakeData();
            }
        }

        public void Dispose()
        {
            if (dataBuffer != null) dataBuffer.Dispose();
            dataBuffer = null;

            if (dataMap != null) GameObject.Destroy(dataMap);
            dataMap = null;

            if (tempDataMap != null) GameObject.Destroy(tempDataMap);
            tempDataMap = null;
        }

        void BakeData()
        {
            //int totalProperties = pointsData.Length * PointDataPropertiesCount;

            // Lazy initialization of temporary objects
            if (dataBuffer == null)
            {
                //dataBuffer = new ComputeBuffer(totalProperties, sizeof(float));
                dataBuffer = new ComputeBuffer(pointsData.Length, sizeof(float) * PointDataPropertiesCount);
            }

            if (dataMap == null)
            {
                dataMap = CreateRenderTexture(mapWidth, mapHeight, false);
            }

            if (tempDataMap == null)
            {
                tempDataMap = CreateRenderTexture(dataMap, true);
            }

            ClearOutRenderTexture(tempDataMap);

            // Set pointsData and execute the bake task.
            baker.SetInt("ValidDataCount", validDataCount);
            baker.SetInt("GroupSizeX", mapWidth);
            dataBuffer.SetData(pointsData);
            baker.SetBuffer(0, "DataBuffer", dataBuffer);
            baker.SetTexture(0, "DataMap", tempDataMap);

            baker.Dispatch(0, mapWidth / 8, mapHeight / 8, 1);

            // once complete, write the results back on to the real pointsData map file
            Graphics.CopyTexture(tempDataMap, dataMap);
        }

        private RenderTexture CreateRenderTexture(int width, int height, bool enableRandomWrite = true)
        {
            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf);
            rt.enableRandomWrite = enableRandomWrite;
            rt.Create();
            return rt;
        }

        private RenderTexture CreateRenderTexture(RenderTexture source, bool enableRandomWrite = true)
        {
            var rt = CreateRenderTexture(source.width, source.height, enableRandomWrite);
            return rt;
        }

        void ClearOutRenderTexture(RenderTexture renderTexture)
        {
            //RenderTexture rt = RenderTexture.active;
            //RenderTexture.active = renderTexture;
            //GL.Clear(true, true, Color.clear);
            //RenderTexture.active = rt;
        }
    }
}

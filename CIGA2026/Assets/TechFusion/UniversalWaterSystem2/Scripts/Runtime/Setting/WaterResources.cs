using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalWaterSystem
{
    [CreateAssetMenu(fileName = "New UWS WaterResources", menuName = "UniversalWaterSystem/WaterResources")]
    public class WaterResources : ScriptableObject
    {
        public Texture2D foamTexture;
        public Texture2D contactFoamTexture;
        public Texture2D bubbleTexture;
        public Texture2D causticsTexture;

        public Shader foamBlendShader;
        public Shader submergeShader;
        public Shader underwaterOpaqueShader;
        public Shader underwaterDistortionShader;
        public Shader waterLineShader;
    }
}

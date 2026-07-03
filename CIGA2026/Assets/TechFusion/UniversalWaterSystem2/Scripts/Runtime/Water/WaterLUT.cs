using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UIElements;

namespace UniversalWaterSystem
{
    public partial class Water : MonoBehaviour
    {
        [SerializeField] private ColorsPreset colorsPreset;

        private Texture2D rampTexture;

        private Texture2D noiseTexture;

        private void InitLUT()
        {
            if (!rampTexture)
                GenerateColorRamp();

            if (!noiseTexture)
                GenerateNoise();

            Shader.SetGlobalTexture("_AbsorptionScatteringRamp", rampTexture);
            Shader.SetGlobalTexture("Water_ScreenNoise", noiseTexture);
        }

        private void GenerateColorRamp()
        {
            if (rampTexture == null)
            { 
                rampTexture = new Texture2D(128, 4, GraphicsFormat.R8G8B8A8_SRGB, TextureCreationFlags.None); 
            }
                
            rampTexture.wrapMode = TextureWrapMode.Clamp;

            var cols = new Color[512];
            for (var i = 0; i < 128; i++)
            {
                cols[i] = colorsPreset._absorptionRamp.Evaluate(i / 128f);
            }
            for (var i = 0; i < 128; i++)
            {
                cols[i + 128] = colorsPreset._scatterRamp.Evaluate(i / 128f);
            }
            rampTexture.SetPixels(cols);
            rampTexture.Apply();
        }

        private void GenerateNoise()
        {
            // For each pixel in the texture...
            float y = 0.0F;

            int pixWidth = 64;
            int pixHeight = 64;
            float scale = 4;

            if (noiseTexture == null)
                noiseTexture = new Texture2D(pixWidth, pixHeight);

            noiseTexture.filterMode = FilterMode.Bilinear;

            Color[] pix = new Color[noiseTexture.width * noiseTexture.height];

            while (y < noiseTexture.height)
            {
                float x = 0.0F;
                while (x < noiseTexture.width)
                {
                    float xCoord = x / noiseTexture.width * scale;
                    float yCoord = y / noiseTexture.height * scale;
                    float sample = Mathf.PerlinNoise(xCoord, yCoord);
                    pix[(int)y * noiseTexture.width + (int)x] = new Color(sample, sample, sample);
                    x++;
                }
                y++;
            }

            // Copy the pixel data to the texture and load it into the GPU.
            noiseTexture.SetPixels(pix);
            noiseTexture.Apply();
        }
    }
}
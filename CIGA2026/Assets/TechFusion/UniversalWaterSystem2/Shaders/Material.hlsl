#if !defined(MATERIAL_INCLUDED)
#define MATERIAL_INCLUDED

TEXTURE2D(_FoamAlbedo);
SAMPLER(sampler_FoamAlbedo);
TEXTURE2D(_FoamBubble);
SAMPLER(sampler_FoamBubble);
TEXTURE2D(_ContactFoamTexture);
SAMPLER(sampler_ContactFoamTexture);

TEXTURE2D(_CausticsTexture);
SAMPLER(sampler_CausticsTexture);

// Surface textures
TEXTURE2D(_AbsorptionScatteringRamp); 
SAMPLER(sampler_AbsorptionScatteringRamp);

//wave cascades
SAMPLER(sampler_linear_clamp);

TEXTURE2D(_Displacement_c0);
SAMPLER(sampler_Displacement_c0);
TEXTURE2D(_Derivatives_c0);
SAMPLER(sampler_Derivatives_c0);
TEXTURE2D(_Turbulence_c0);
SAMPLER(sampler_Turbulence_c0);

TEXTURE2D(_Displacement_c1);
SAMPLER(sampler_Displacement_c1);
TEXTURE2D(_Derivatives_c1);
SAMPLER(sampler_Derivatives_c1);
TEXTURE2D(_Turbulence_c1);
SAMPLER(sampler_Turbulence_c1);

TEXTURE2D(_Displacement_c2);
SAMPLER(sampler_Displacement_c2);
TEXTURE2D(_Derivatives_c2);
SAMPLER(sampler_Derivatives_c2);
TEXTURE2D(_Turbulence_c2);
SAMPLER(sampler_Turbulence_c2);

float4 Water_Time; //x:time y:delta

//underwater
float3 Water_UnderBaseColor;
float4 Water_UnderSSSColor;

// camera
float4x4 Water_InverseViewMatrix;
float4x4 Water_InverseProjectionMatrix;

CBUFFER_START(UnityPerMaterial)
//scale
float _GeometryScale;

float LengthScale0;
float LengthScale1;
float LengthScale2;
float _LOD_scale;
float _SSSBase;
float _SSSScale;
float _SSSStrength;

// foam
//float _FoamBiasLOD0;
//float _FoamBiasLOD1;
//float _FoamBiasLOD2;
float _FoamScale;
float _ContactFoam;

//reflection
float _ReflectionStrength;

// depth
half _MaxDepth;

//fade
float _NormalFadeFar;
float _FoamFadeFar;
CBUFFER_END

#endif
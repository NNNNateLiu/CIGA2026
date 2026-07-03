#ifndef CASCADE_INCLUDED
#define CASCADE_INCLUDED

#define MAX_LOD_COUNT 15

float4 Water_CascadeSize[MAX_LOD_COUNT + 1]; // x:texel width, y:texture res, z: 1, w:1 / texture res
float4 Water_CascadePosScale[MAX_LOD_COUNT + 1];

float4 Water_PrevCascadeSize[MAX_LOD_COUNT + 1]; // x:texel width, y:texture res, z: 1, w:1 / texture res
float4 Water_PrevCascadePosScale[MAX_LOD_COUNT + 1];

Texture2DArray Water_DynamicDisplacement;
Texture2DArray Water_CascadeFoam;
SamplerState samplerWater_DynamicDisplacement_linear_clamp;

float2 WorldToUV(float2 worldXZ, float2 centerPos, float res, float texelSize)
{
	return (worldXZ - centerPos) / (texelSize * res) + 0.5;
}

float3 WorldToUV(float2 worldXZ, uint sliceIndex) {
	const float2 result = WorldToUV(
		worldXZ,
		Water_CascadePosScale[sliceIndex].xy,
		Water_CascadeSize[sliceIndex].y,
		Water_CascadeSize[sliceIndex].x
	);
	return float3(result, sliceIndex);
}

float3 WorldToPrevUV(float2 worldXZ, uint sliceIndex) {
	const float2 result = WorldToUV(
		worldXZ,
		Water_PrevCascadePosScale[sliceIndex].xy,
		Water_PrevCascadeSize[sliceIndex].y,
		Water_PrevCascadeSize[sliceIndex].x
	);
	return float3(result, sliceIndex);
}

float3 WorldToUV(float2 worldXZ)
{
	float3 uv = WorldToUV(worldXZ, 0);

	if (max(abs(uv.x - 0.5), abs(uv.y - 0.5)) > 0.5)
	{
		uv = WorldToUV(worldXZ, 1);
	}

	if (max(abs(uv.x - 0.5), abs(uv.y - 0.5)) > 0.5)
	{
		uv = WorldToUV(worldXZ, 2);
	}

	if (max(abs(uv.x - 0.5), abs(uv.y - 0.5)) > 0.5)
	{
		uv = WorldToUV(worldXZ, 3);
	}

	return uv;
}

float3 WorldToPrevUV(float2 worldXZ) {
	float3 uv = WorldToPrevUV(worldXZ, 0);

	if (max(abs(uv.x - 0.5), abs(uv.y - 0.5)) > 0.5)
	{
		uv = WorldToPrevUV(worldXZ, 1);
	}

	if (max(abs(uv.x - 0.5), abs(uv.y - 0.5)) > 0.5)
	{
		uv = WorldToPrevUV(worldXZ, 2);
	}

	if (max(abs(uv.x - 0.5), abs(uv.y - 0.5)) > 0.5)
	{
		uv = WorldToPrevUV(worldXZ, 3);
	}

	return uv;
}

float UVBoundFlag(float2 uv)
{
	return max(sign(0.5 - max(abs(uv.x - 0.5), abs(uv.y - 0.5))), 0);
}

float2 UVToWorld(float2 uv, float2 centerPos, float res, float texelSize)
{
	return texelSize * res * (uv - 0.5) + centerPos;
}

float2 UVToWorld(float2 uv, float sliceIndex) { 
	return UVToWorld(
		uv, 
		Water_CascadePosScale[sliceIndex].xy,
		Water_CascadeSize[sliceIndex].y,
		Water_CascadeSize[sliceIndex].x);
}

float2 PrevUVToWorld(float2 uv, float sliceIndex) {
	return UVToWorld(
		uv,
		Water_PrevCascadePosScale[sliceIndex].xy,
		Water_PrevCascadeSize[sliceIndex].y,
		Water_PrevCascadeSize[sliceIndex].x);
}

float4 SampleWaveDisplacement(float2 worldXZ)
{
	float4 displacement = 0;
	//float3 uv = WorldToUV(worldXZ, 0);

	//if (max(abs(uv.x - 0.5), abs(uv.y - 0.5)) > 0.5)
	//{
	//	uv = WorldToUV(worldXZ, 1);
	//}

	////if (max(abs(uv.x - 0.5), abs(uv.y - 0.5)) > 0.5)
	////{
	////	uv = WorldToUV(worldXZ, 2);
	////}

	float3 uv = WorldToUV(worldXZ);
    displacement += Water_DynamicDisplacement.SampleLevel(samplerWater_DynamicDisplacement_linear_clamp, uv, 0) * UVBoundFlag(uv.xy);

	return displacement;
}

//todo, fix sliceIndex
float3 SampleWaveNormal(float2 worldXZ, uint sliceIndex)
{
	float3 normal = float3(0, 1, 0);

	float2 dd = float2(0.0, Water_CascadeSize[sliceIndex].x);
	
	float3 disp = SampleWaveDisplacement(worldXZ).xyz;
	float3 disp_x = dd.yxx + SampleWaveDisplacement(worldXZ + dd.yx).xyz;
	float3 disp_z = dd.xxy + SampleWaveDisplacement(worldXZ + dd.xy).xyz;
	normal = normalize(cross(disp_z - disp, disp_x - disp));

	return normal;
}

float2 SampleWaveFoam(float2 worldXZ)
{
    float3 uv = WorldToUV(worldXZ);

    float2 foam = Water_CascadeFoam.SampleLevel(samplerWater_DynamicDisplacement_linear_clamp, uv, 0).rg * UVBoundFlag(uv.xy);

    return foam;
}

#endif // CASCADE_INCLUDED
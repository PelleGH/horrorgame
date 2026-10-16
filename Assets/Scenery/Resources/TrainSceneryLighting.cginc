#ifndef TRAIN_SCENERY_LIGHTING_INCLUDED
#define TRAIN_SCENERY_LIGHTING_INCLUDED
float _UseSceneLighting;
float4 _TrainSceneryAmbient;
int _TrainSceneryBeamCount;
float4 _TrainSceneryBeamPositions[8];
float4 _TrainSceneryBeamDirections[8];
float4 _TrainSceneryBeamColors[8];
float3 TrainBeamLight(float3 worldPosition, float3 normal, float surface)
{
    float hemisphere = lerp(1,.6+.4*saturate(normal.y*.5+.5),surface);
    float moon = saturate(dot(normal,normalize(float3(-.3,.8,.4))))*.28*surface;
    float3 light = _TrainSceneryAmbient.rgb * (hemisphere + moon);
    [unroll] for (int i = 0; i < 8; i++)
    {
        if (i >= _TrainSceneryBeamCount) break;
        float3 offset = worldPosition - _TrainSceneryBeamPositions[i].xyz;
        float distance = max(.01, length(offset));
        float3 direction = offset / distance;
        float angle = dot(direction, _TrainSceneryBeamDirections[i].xyz);
        // Point proxies use a broad, smoothly fading window hemisphere, not a spotlight cone.
        float cone = _TrainSceneryBeamDirections[i].w < 0
            ? smoothstep(-.25, .6, angle)
            : smoothstep(_TrainSceneryBeamDirections[i].w, lerp(_TrainSceneryBeamDirections[i].w, 1, .55), angle);
        float attenuation = saturate(1 - distance / _TrainSceneryBeamPositions[i].w);
        float diffuse = lerp(1, .2 + .8 * saturate(dot(normal, -direction)), surface);
        light += _TrainSceneryBeamColors[i].rgb * cone * attenuation * attenuation * diffuse;
    }
    return light;
}
#endif

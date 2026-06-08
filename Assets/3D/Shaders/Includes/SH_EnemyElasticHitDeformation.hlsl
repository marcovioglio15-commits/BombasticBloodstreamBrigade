#ifndef BOMBASTIC_BLOODSTREAM_BRIGADE_ENEMY_ELASTIC_HIT_DEFORMATION_INCLUDED
#define BOMBASTIC_BLOODSTREAM_BRIGADE_ENEMY_ELASTIC_HIT_DEFORMATION_INCLUDED

// Applies a damped squash-and-stretch response in object space while gameplay transforms remain unchanged.
void ApplyEnemyElasticHitDeformation(inout float3 positionOS,
                                     inout float3 normalOS,
                                     float3 directionOS,
                                     float4 timingProperty,
                                     float4 motionProperty)
{
    float durationSeconds = max(0.0, timingProperty.y);

    if (durationSeconds <= 0.0001)
        return;

    float normalizedTime = saturate((_Time.y - timingProperty.x) / durationSeconds);

    if (normalizedTime >= 1.0)
        return;

    float maximumCompression = saturate(timingProperty.z);
    float volumeCompensation = saturate(timingProperty.w);
    float oscillationCount = max(0.01, motionProperty.x);
    float damping = max(0.0, motionProperty.y);
    float directionality = saturate(motionProperty.z);
    float wave = exp(-damping * normalizedTime) * cos(6.28318530718 * oscillationCount * normalizedTime);
    float compression = clamp(maximumCompression * wave, -0.65, 0.75);
    float axisScale = max(0.25, 1.0 - compression);
    float perpendicularScale = max(0.25, 1.0 + compression * volumeCompensation);
    directionOS.y = 0.0;

    if (dot(directionOS, directionOS) <= 0.0001)
        directionOS = float3(0.0, 0.0, 1.0);

    directionOS = normalize(directionOS);
    float axisDistance = dot(positionOS, directionOS);
    float3 axisPosition = directionOS * axisDistance;
    float3 perpendicularPosition = positionOS - axisPosition;
    float3 directionalPosition = axisPosition * axisScale + perpendicularPosition * perpendicularScale;
    float3 broadPosition = float3(positionOS.x * perpendicularScale,
                                  positionOS.y * axisScale,
                                  positionOS.z * perpendicularScale);
    float3 deformedPosition = lerp(broadPosition, directionalPosition, directionality);

    if (motionProperty.w > 0.5 && positionOS.y >= 0.0)
        deformedPosition.y = max(0.0, deformedPosition.y);

    float normalAxisDistance = dot(normalOS, directionOS);
    float3 directionalNormal = directionOS * normalAxisDistance / axisScale +
                               (normalOS - directionOS * normalAxisDistance) / perpendicularScale;
    float3 broadNormal = float3(normalOS.x / perpendicularScale,
                                normalOS.y / axisScale,
                                normalOS.z / perpendicularScale);
    positionOS = deformedPosition;
    normalOS = normalize(lerp(broadNormal, directionalNormal, directionality));
}

#endif

#ifndef CANAAN_ATMOSPHERE_INCLUDED
#define CANAAN_ATMOSPHERE_INCLUDED

// The air of the game: an exponential height fog plus a uniform haze, lit from the side of the sun.
// Opaque surfaces get it from the full-screen pass Canaan/Atmosphere; transparent shaders call CanaanAtmo
// themselves. All values come from SkyWeather, so the sky, the fog and the light always agree.

float4 _AtmoFogColor;   // rgb: light scattered in from the whole sky (linear), a: haze density per metre
float4 _AtmoSunColor;   // rgb: extra light scattered in when looking towards the sun or moon, a: lobe exponent
float4 _AtmoHeight;     // x: base height, y: fall-off per metre, z: density at the base height, w: opacity limit
float4 _AtmoSunDir;     // xyz: direction to the sun or moon, w: strength of the light shafts

// How much of the view ray the air swallows, 0..limit.
half CanaanFogAmount(float3 camPos, float3 dir, float dist)
{
    float k = _AtmoHeight.y;
    float atCam = exp(-clamp(camPos.y - _AtmoHeight.x, -30.0, 4000.0) * k);
    float x = clamp(dir.y * dist * k, -16.0, 60.0);
    float along = abs(x) > 1e-3 ? (1.0 - exp(-x)) / x : 1.0;
    float depth = (_AtmoHeight.z * atCam * along + _AtmoFogColor.a) * dist;
    return min(1.0 - exp(-depth), _AtmoHeight.w);
}

// The colour of the air in a direction: brighter and warmer towards the light.
half3 CanaanFogColor(float3 dir, half lit)
{
    half s = saturate(dot(dir, _AtmoSunDir.xyz));
    half lobe = pow(s, _AtmoSunColor.a) + 0.18 * s * s;
    return _AtmoFogColor.rgb + _AtmoSunColor.rgb * (lobe * lit);
}

// rgb: light scattered in (already weighted), a: share of the surface colour that survives.
half4 CanaanAtmo(float3 positionWS)
{
    float3 v = positionWS - _WorldSpaceCameraPos;
    float dist = length(v);
    float3 dir = v / max(dist, 1e-4);
    half f = CanaanFogAmount(_WorldSpaceCameraPos, dir, dist);
    return half4(CanaanFogColor(dir, 1.0) * f, 1.0 - f);
}

half3 CanaanApplyAtmo(half3 color, float3 positionWS)
{
    half4 a = CanaanAtmo(positionWS);
    return color * a.a + a.rgb;
}

#endif

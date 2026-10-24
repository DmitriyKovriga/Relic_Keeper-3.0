Shader "RelicKeeper/Atmosphere/Depth Mist"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Blend One OneMinusSrcAlpha
            Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RelicMist.hlsl"
            #include "WorldColorGrade.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _MistColor;
                float4 _MistParameters; // opacity, shafts, noise scale, quality
                float4 _MistDrift; // world drift.xy, PPU, intensity
                float4 _MistBand; // centre Y, half-height, feather, wisp strength
                float4 _MistOrigin; // room anchor.xy, limit height band
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 world : TEXCOORD0; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS);
                o.world = TransformObjectToWorld(i.positionOS).xy;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float ppu = max(1.0, _MistDrift.z);
                float2 world = (floor(i.world * ppu) + 0.5) / ppu;
                float belt = smoothstep(0.0, 1.0, saturate((_MistBand.y - abs(world.y - _MistBand.x)) / max(0.01, _MistBand.z)));
                belt = lerp(1.0, belt, _MistOrigin.z);
                float2 sampleWorld = world - _MistOrigin.xy;
                float2 drift = floor(_MistDrift.xy * ppu) / ppu;
                float fog = RelicWisps(sampleWorld, drift * _MistParameters.z, _MistParameters.z, _MistParameters.w);
                float alpha = _MistParameters.x * lerp(1.0, fog, _MistBand.w) * _MistDrift.w * belt;
                // Broad, interrupted diagonal shafts on the background plane, behind all gameplay.
                float beam = pow(saturate(sin((world.x + world.y * 0.32) * 0.75 + 1.2)), 12.0);
                beam *= (0.35 + 0.65 * RelicNoise(world * 0.18 + drift * 0.04));
                half3 tint = RKGradeColor(_MistColor.rgb, 3.0, 1.0, 0.0);
                half3 color = tint * alpha;
                color += tint * beam * _MistParameters.y * _MistDrift.w * belt;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

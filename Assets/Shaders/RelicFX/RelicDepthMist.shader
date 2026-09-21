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
            CBUFFER_START(UnityPerMaterial)
                float4 _MistColor;
                float4 _MistParameters; // opacity, shafts, noise scale, quality
                float4 _MistDrift; // world drift.xy, PPU, intensity
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
                float2 drift = floor(_MistDrift.xy * ppu) / ppu;
                float fog = RelicWisps(world, drift * _MistParameters.z, _MistParameters.z, _MistParameters.w);
                float alpha = _MistParameters.x * (0.26 + 0.74 * fog) * _MistDrift.w;
                // Broad, interrupted diagonal shafts on the background plane, behind all gameplay.
                float beam = pow(saturate(sin((world.x + world.y * 0.32) * 0.75 + 1.2)), 12.0);
                beam *= (0.35 + 0.65 * RelicNoise(world * 0.18 + drift * 0.04));
                half3 color = _MistColor.rgb * alpha;
                color += _MistColor.rgb * beam * _MistParameters.y * _MistDrift.w;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

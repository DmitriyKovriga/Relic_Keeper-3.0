Shader "Hidden/RelicKeeper/Atmosphere"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Relic Atmosphere"
            ZWrite Off ZTest Always Cull Off
            Blend One OneMinusSrcAlpha
            ColorMask RGB
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4x4 _AtmosphereInverseVP;
                float4 _AtmosphereReveal; // center.xy, clear radius, strength
                float4 _AtmosphereWorld;  // darkness, fog, vignette, intensity
                float4 _AtmosphereFog;
                float4 _AtmosphereNoise;  // drift.xy, scale, amount
                float4 _AtmosphereGrid;   // logical resolution.xy, PPU, dither
                float4 _AtmosphereShape;  // softness, world Z, quality, pulse
            CBUFFER_END

            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                o.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                o.uv = GetFullScreenTriangleTexCoord(vertexID);
                return o;
            }

            float Hash(float2 p)
            {
                // Deterministic spatial hash; no time-dependent seeds.
                float3 q = frac(float3(p.x, p.y, p.x) * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p), t = frac(p);
                t = t * t * (3.0 - 2.0 * t);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), t.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), t.x), t.y);
            }

            float OrderedDither(int2 p)
            {
                // Bayer 4x4, generated from interleaved two-bit coordinates.
                uint x = (uint)p.x & 3u, y = (uint)p.y & 3u;
                uint low = ((x & 1u) ^ (y & 1u)) * 2u + (y & 1u);
                uint high = (((x >> 1u) & 1u) ^ ((y >> 1u) & 1u)) * 2u + ((y >> 1u) & 1u);
                return (low * 4u + high + 0.5) / 16.0 - 0.5;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 a = ComputeWorldSpacePosition(i.uv, 0.0, _AtmosphereInverseVP);
                float3 b = ComputeWorldSpacePosition(i.uv, 1.0, _AtmosphereInverseVP);
                float3 world = a + (b - a) * ((_AtmosphereShape.y - a.z) / (b.z - a.z));
                float ppu = _AtmosphereGrid.z;
                float2 cell = floor(world.xy * ppu);
                float2 xy = (cell + 0.5) / ppu;
                float distanceToPlayer = distance(xy, _AtmosphereReveal.xy);
                float reveal = (1.0 - smoothstep(_AtmosphereReveal.z,
                    _AtmosphereReveal.z + _AtmosphereShape.x, distanceToPlayer)) * _AtmosphereReveal.w;
                float strength = saturate(_AtmosphereWorld.w * _AtmosphereShape.w) * (1.0 - reveal);
                float noise = 0.5;
                if (_AtmosphereShape.z > 0.5 && _AtmosphereNoise.w > 0.0)
                {
                    float2 drift = floor(_AtmosphereNoise.xy * ppu) / ppu;
                    float2 p = (xy + drift) * _AtmosphereNoise.z;
                    noise = Noise(p);
                    if (_AtmosphereShape.z > 1.5) noise = noise * 0.7 + Noise(p * 2.03 + 7.0) * 0.3;
                }
                float fog = saturate(_AtmosphereWorld.y * strength * (1.0 + (noise * 2.0 - 1.0) * _AtmosphereNoise.w));
                float2 screen = (floor(i.uv * _AtmosphereGrid.xy) + 0.5) / _AtmosphereGrid.xy;
                float vignette = _AtmosphereWorld.z * strength * smoothstep(0.15, 0.72, length(screen - 0.5));
                float dark = _AtmosphereWorld.x * strength;
                float alpha = 1.0 - (1.0 - dark) * (1.0 - fog) * (1.0 - vignette);
                float dither = _AtmosphereShape.z > 0.5 ? OrderedDither((int2)cell) * _AtmosphereGrid.w / 64.0 : 0.0;
                float quantized = saturate(floor(saturate(alpha + dither) * 255.0 + 0.5) / 255.0);
                // Premultiplied fog + black attenuation, folded into one blend operation.
                half3 color = _AtmosphereFog.rgb * fog * (1.0 - vignette);
                color *= alpha > 0.00001 ? quantized / alpha : 0.0;
                return half4(color, quantized);
            }
            ENDHLSL
        }
    }
}

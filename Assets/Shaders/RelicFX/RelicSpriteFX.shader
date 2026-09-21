Shader "RelicKeeper/Sprites/Relic Sprite FX"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _FxTint ("Tint", Color) = (1,1,1,1)
        _FxBrightness ("Brightness", Range(0,2)) = 1
        _FxContrast ("Contrast", Range(0,2)) = 1
        _FxDesaturate ("Desaturate", Range(0,1)) = 0
        _FxFlashColor ("Flash color", Color) = (1,0.2,0.1,1)
        _FxFlash ("Flash amount", Range(0,1)) = 0
        _FxOutlineColor ("Outline color", Color) = (0.2,0.8,1,1)
        _FxOutline ("Outline opacity", Range(0,1)) = 0
        [IntRange] _FxOutlineWidth ("Outline pixels", Range(1,4)) = 1
        _FxInnerColor ("Inner shadow color", Color) = (0,0,0,1)
        _FxInnerShadow ("Inner shadow", Range(0,1)) = 0
        [HDR] _FxEmissionColor ("Emission color", Color) = (0.3,0.8,1,1)
        _FxEmission ("Emission strength", Range(0,4)) = 0
        _FxNoiseTex ("Dissolve noise (point / repeat)", 2D) = "gray" {}
        _FxNoiseScale ("Noise tiling", Range(0.25,8)) = 1
        _FxDissolve ("Dissolve", Range(0,1)) = 0
        [HDR] _FxEdgeColor ("Dissolve edge color", Color) = (1,0.4,0.05,1)
        _FxEdgeWidth ("Dissolve edge width", Range(0,0.25)) = 0.05
        _FxFade ("Alpha fade", Range(0,1)) = 1
        _FxClip ("Alpha clip", Range(0,1)) = 0
        [Enum(Low,0,Medium,1,High,2)] _FxQuality ("Quality", Float) = 1
        // Legacy fade alias: alpha is combined with renderer alpha by min, never multiplied twice.
        [HideInInspector] _RendererColor ("Legacy renderer fade", Color) = (1,1,1,1)
        [HideInInspector] _FxUvRect ("Atlas UV bounds", Vector) = (0,0,1,1)
        [HideInInspector] _FxNeighboursAllowed ("Safe atlas neighbours", Float) = 0
        [HideInInspector] _FxRuntimeTint ("Runtime tint", Color) = (1,1,1,1)
        [HideInInspector] _FxRuntimeFlashColor ("Runtime flash color", Color) = (1,1,1,1)
        [HideInInspector] _FxRuntimeFlash ("Runtime flash", Float) = 0
        [HideInInspector] _FxRuntimeEmissionColor ("Runtime emission color", Color) = (1,1,1,1)
        [HideInInspector] _FxRuntimeEmission ("Runtime emission", Float) = 0
        [HideInInspector] _FxRuntimeDissolve ("Runtime dissolve", Float) = 0
        [HideInInspector] _FxRuntimeFade ("Runtime fade", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            Name "Relic Sprite FX"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_FxNoiseTex); SAMPLER(sampler_FxNoiseTex);
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _FxTint, _FxFlashColor, _FxOutlineColor, _FxInnerColor, _FxEmissionColor, _FxEdgeColor;
                half4 _RendererColor, _FxRuntimeTint, _FxRuntimeFlashColor, _FxRuntimeEmissionColor;
                float4 _FxUvRect;
                float _FxBrightness, _FxContrast, _FxDesaturate, _FxFlash, _FxOutline, _FxOutlineWidth;
                float _FxInnerShadow, _FxEmission, _FxNoiseScale, _FxDissolve, _FxEdgeWidth, _FxFade, _FxClip, _FxQuality;
                float _FxNeighboursAllowed, _FxRuntimeFlash, _FxRuntimeEmission, _FxRuntimeDissolve, _FxRuntimeFade;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                SetUpSpriteInstanceProperties();
                o.positionCS = TransformObjectToHClip(UnityFlipSprite(input.positionOS, unity_SpriteProps.xy));
                o.uv = input.uv;
                o.color = input.color * unity_SpriteColor;
                return o;
            }

            half4 SpriteSample(float2 uv)
            {
                float2 halfTexel = _MainTex_TexelSize.xy * 0.5;
                float2 inside = step(_FxUvRect.xy, uv) * step(uv, _FxUvRect.zw);
                float2 safeUV = clamp(uv, _FxUvRect.xy + halfTexel, _FxUvRect.zw - halfTexel);
                half4 value = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, safeUV);
                value.a *= inside.x * inside.y;
                return value;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 texel = SpriteSample(i.uv);
                half3 rgb = texel.rgb * i.color.rgb * _FxTint.rgb * _FxRuntimeTint.rgb;
                float luminance = dot(rgb, half3(0.2126, 0.7152, 0.0722));
                rgb = lerp(rgb, luminance.xxx, _FxDesaturate);
                rgb = max(0, (rgb - 0.5) * _FxContrast + 0.5) * _FxBrightness;

                float dilation = texel.a, erosion = texel.a;
                if (_FxNeighboursAllowed > 0.5 && (_FxOutline > 0.0 || _FxInnerShadow > 0.0))
                {
                    int width = (int)clamp(round(_FxOutlineWidth), 1.0, 4.0);
                    // Low: one cardinal ring. Medium: cardinal rings. High: adds diagonals.
                    if (_FxQuality < 0.5) width = 1;
                    [loop] for (int r = 1; r <= width; r++)
                    {
                        float2 d = _MainTex_TexelSize.xy * r;
                        float a = SpriteSample(i.uv + float2(d.x, 0)).a;
                        float b = SpriteSample(i.uv - float2(d.x, 0)).a;
                        float c = SpriteSample(i.uv + float2(0, d.y)).a;
                        float e = SpriteSample(i.uv - float2(0, d.y)).a;
                        dilation = max(dilation, max(max(a,b), max(c,e)));
                        if (r == 1) erosion = min(erosion, min(min(a,b), min(c,e)));
                        if (_FxQuality > 1.5)
                        {
                            float f = SpriteSample(i.uv + d).a;
                            float g = SpriteSample(i.uv - d).a;
                            float h = SpriteSample(i.uv + float2(d.x, -d.y)).a;
                            float j = SpriteSample(i.uv + float2(-d.x, d.y)).a;
                            dilation = max(dilation, max(max(f,g), max(h,j)));
                        }
                    }
                }
                rgb = lerp(rgb, _FxInnerColor.rgb, saturate(texel.a - erosion) * _FxInnerShadow * _FxInnerColor.a);
                rgb = lerp(rgb, _FxFlashColor.rgb, saturate(_FxFlash));
                rgb = lerp(rgb, _FxRuntimeFlashColor.rgb, saturate(_FxRuntimeFlash));
                rgb += _FxEmissionColor.rgb * _FxEmission + _FxRuntimeEmissionColor.rgb * _FxRuntimeEmission;

                float outlineAlpha = saturate(dilation - texel.a) * _FxOutline * _FxOutlineColor.a;
                float coverage = texel.a + outlineAlpha;
                rgb = (rgb * texel.a + _FxOutlineColor.rgb * outlineAlpha) / max(coverage, 0.00001);

                float dissolve = max(_FxDissolve, _FxRuntimeDissolve);
                if (dissolve > 0.0)
                {
                    // Sprite-local UV keeps the noise independent of atlas placement. No temporal seed.
                    float2 localUV = (i.uv - _FxUvRect.xy) / max(_FxUvRect.zw - _FxUvRect.xy, 0.000001);
                    float noise = SAMPLE_TEXTURE2D(_FxNoiseTex, sampler_FxNoiseTex, localUV * _FxNoiseScale).r;
                    float remaining = noise - dissolve;
                    coverage *= step(0.0, remaining) * (1.0 - step(1.0, dissolve));
                    float edge = _FxEdgeWidth > 0.0 ? 1.0 - step(_FxEdgeWidth, remaining) : 0.0;
                    rgb = lerp(rgb, _FxEdgeColor.rgb, edge * _FxEdgeColor.a);
                }
                float alpha = coverage * min(i.color.a, _RendererColor.a) * _FxTint.a * _FxRuntimeTint.a * _FxFade * _FxRuntimeFade;
                if (_FxClip > 0.0) clip(alpha - _FxClip);
                return half4(rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
    CustomEditor "RelicKeeper.Editor.Visuals.RelicSpriteFxShaderGUI"
}

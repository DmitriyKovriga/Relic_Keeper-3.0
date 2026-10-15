Shader "RelicKeeper/ElectricBeam"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("External Alpha Enabled", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment SpriteFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            ENDCG
        }
    }
}

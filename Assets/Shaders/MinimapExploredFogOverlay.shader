Shader "RTS/Minimap/Explored Fog Overlay"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _ExploredTex("Explored Texture", 2D) = "black" {}
        _VisionTex("Vision Texture", 2D) = "black" {}
        _UnexploredColor("Unexplored Color", Color) = (0, 0, 0, 1)
        _ExploredFogColor("Explored Fog Color", Color) = (0, 0, 0, 0.75)
        _ExploredThreshold("Explored Threshold", Range(0, 1)) = 0.1
        _VisionThreshold("Vision Threshold", Range(0, 1)) = 0.9
        _FogUvBottomLeft("Fog UV Bottom Left", Vector) = (0, 0, 0, 0)
        _FogUvTopRight("Fog UV Top Right", Vector) = (1, 1, 0, 0)
        _VisionFogUvBottomLeft("Vision Fog UV Bottom Left", Vector) = (0, 0, 0, 0)
        _VisionFogUvTopRight("Vision Fog UV Top Right", Vector) = (1, 1, 0, 0)

        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _ExploredTex;
            sampler2D _VisionTex;
            fixed4 _UnexploredColor;
            fixed4 _ExploredFogColor;
            fixed _ExploredThreshold;
            fixed _VisionThreshold;
            float4 _FogUvBottomLeft;
            float4 _FogUvTopRight;
            float4 _VisionFogUvBottomLeft;
            float4 _VisionFogUvTopRight;
            float4 _ClipRect;
            float4 _MainTex_ST;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 worldUV : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.worldUV = input.texcoord;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 exploredUv = saturate(lerp(_FogUvBottomLeft.xy, _FogUvTopRight.xy, input.worldUV));
                float2 visionUv = saturate(lerp(_VisionFogUvBottomLeft.xy, _VisionFogUvTopRight.xy, input.worldUV));
                fixed explored = tex2D(_ExploredTex, exploredUv).r;
                fixed vision = tex2D(_VisionTex, visionUv).r;

                fixed hasVision = step(_VisionThreshold, vision);
                fixed isUnexplored = step(explored, _ExploredThreshold);
                fixed isExplored = 1.0 - isUnexplored;

                fixed4 result = fixed4(0, 0, 0, 0);
                result = lerp(result, _UnexploredColor, isUnexplored * (1.0 - hasVision));
                result = lerp(result, _ExploredFogColor, isExplored * (1.0 - hasVision));

                result.a *= input.color.a;
                result.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                return result;
            }
            ENDCG
        }
    }
}

Shader "Custom/SpriteOutlineGradient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _Color ("Tint", Color) = (1, 1, 1, 1)

        _OutlineColor ("Outline Color", Color) = (1, 0, 0, 1)
        _OutlineSize ("Outline Size", Range(0, 8)) = 2
        _AlphaThreshold ("Alpha Threshold", Range(0, 1)) = 0.001
        _OutlineSoftness ("Outline Softness", Range(0.001, 1)) = 0.15
        _SpriteEdgeSoftness ("Sprite Edge Softness", Range(0.001, 1)) = 0.2
        _GradientPower ("Gradient Power", Range(0.2, 4)) = 1.0

        [MaterialToggle] PixelSnap ("Pixel Snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ PIXELSNAP_ON

            #include "UnityCG.cginc"

            struct AppData
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct VertexToFragment
            {
                float4 vertex : SV_POSITION;
                fixed4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineSize;
            float _AlphaThreshold;
            float _OutlineSoftness;
            float _SpriteEdgeSoftness;
            float _GradientPower;

            VertexToFragment vert(AppData input)
            {
                VertexToFragment output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;

                #ifdef PIXELSNAP_ON
                    output.vertex = UnityPixelSnap(output.vertex);
                #endif

                return output;
            }

            float SampleMaxNeighborAlpha(float2 uv, float2 radiusOffset)
            {
                float maxAlpha = 0.0;

                float2 dirs[16] =
                {
                    float2( 1.0000,  0.0000),
                    float2( 0.9239,  0.3827),
                    float2( 0.7071,  0.7071),
                    float2( 0.3827,  0.9239),
                    float2( 0.0000,  1.0000),
                    float2(-0.3827,  0.9239),
                    float2(-0.7071,  0.7071),
                    float2(-0.9239,  0.3827),
                    float2(-1.0000,  0.0000),
                    float2(-0.9239, -0.3827),
                    float2(-0.7071, -0.7071),
                    float2(-0.3827, -0.9239),
                    float2( 0.0000, -1.0000),
                    float2( 0.3827, -0.9239),
                    float2( 0.7071, -0.7071),
                    float2( 0.9239, -0.3827)
                };

                [unroll]
                for (int i = 0; i < 16; i++)
                {
                    float a = tex2D(_MainTex, uv + dirs[i] * radiusOffset).a;
                    maxAlpha = max(maxAlpha, a);
                }

                return maxAlpha;
            }

            fixed4 frag(VertexToFragment input) : SV_Target
            {
                fixed4 textureColor = tex2D(_MainTex, input.uv);
                fixed4 spriteColor = textureColor * input.color;

                // Soft sprite mask so semi-transparent shadow blends smoothly into outline
                float spriteMask = smoothstep(
                    _AlphaThreshold,
                    _AlphaThreshold + _SpriteEdgeSoftness,
                    textureColor.a
                );

                float outlineGradient = 0.0;

                if (_OutlineSize > 0.001)
                {
                    const int MAX_STEPS = 8;
                    int stepCount = clamp((int)ceil(_OutlineSize), 1, MAX_STEPS);

                    [unroll]
                    for (int s = 1; s <= MAX_STEPS; s++)
                    {
                        if (s > stepCount)
                            break;

                        // Radius increases outward
                        float radiusT = (float)s / (float)stepCount;
                        float radius = _OutlineSize * radiusT;

                        // First ring = strongest, last ring = weakest
                        float weightT = (stepCount <= 1) ? 0.0 : (float)(s - 1) / (float)(stepCount - 1);
                        float weight = pow(saturate(1.0 - weightT), _GradientPower);

                        float ringAlpha = SampleMaxNeighborAlpha(
                            input.uv,
                            _MainTex_TexelSize.xy * radius
                        );

                        float ringMask = smoothstep(
                            _AlphaThreshold,
                            _AlphaThreshold + _OutlineSoftness,
                            ringAlpha
                        );

                        outlineGradient = max(outlineGradient, ringMask * weight);
                    }
                }

                // Only show outline outside the sprite, but let it blend softly near shadow edges
                float outlineBaseAlpha =
                    outlineGradient *
                    (1.0 - spriteMask) *
                    _OutlineColor.a *
                    input.color.a;

                // Put outline behind the sprite
                float visibleOutlineAlpha = outlineBaseAlpha * (1.0 - spriteColor.a);

                float finalAlpha = spriteColor.a + visibleOutlineAlpha;

                float3 finalRgb = 0;
                if (finalAlpha > 0.0001)
                {
                    float3 premul =
                        spriteColor.rgb * spriteColor.a +
                        _OutlineColor.rgb * visibleOutlineAlpha;

                    finalRgb = premul / finalAlpha;
                }

                return fixed4(finalRgb, finalAlpha);
            }

            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
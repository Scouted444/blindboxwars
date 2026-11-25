// Soft-toon lit shader for Blind Box Wars (Built-in render pipeline).
// - Vertex colours carry the albedo, so whole buildings are one mesh / one material.
// - Vertex alpha < 1 marks "glow" surfaces (windows, lamps, headlights) that light up at night via the global _BBGlow.
// - Hemisphere ambient from globals _BBSky / _BBGround, soft-banded sun, real shadows, fog, point lights.
Shader "BB/Toon"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _Soft ("Light band softness", Range(0.02, 1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _Color;
            fixed4 _EmissionColor;
            half _Soft;
            float4 _BBSky;
            float4 _BBGround;
            float4 _BBGlowColor;
            float _BBGlow;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wn : TEXCOORD0;
                fixed4 col : COLOR;
                float3 wp : TEXCOORD1;
                SHADOW_COORDS(2)
                UNITY_FOG_COORDS(3)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.col = v.color * _Color;
                o.col.a = v.color.a;
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.wn);
                float ndl = dot(n, _WorldSpaceLightPos0.xyz);
                float sh = SHADOW_ATTENUATION(i);
                float lit = smoothstep(-0.05, _Soft, ndl) * sh;
                float3 amb = lerp(_BBGround.rgb, _BBSky.rgb, n.y * 0.5 + 0.5);
                float3 c = i.col.rgb * (amb + _LightColor0.rgb * lit);
                // gentle rim to separate silhouettes like a stylised game
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                float rim = pow(1 - saturate(dot(n, v)), 4) * 0.12;
                c += rim * _BBSky.rgb;
                // vertex alpha < 0.25: warm window glow;  0.25..0.75: glows in its own colour (lamps, lights, neon)
                float a = i.col.a;
                float gm = step(a, 0.75) * _BBGlow;
                float3 target = a < 0.25 ? _BBGlowColor.rgb * 1.5 : i.col.rgb * 1.9;
                c = lerp(c, target, saturate(gm));
                c += _EmissionColor.rgb;
                fixed4 outc = fixed4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, outc);
                return outc;
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode"="ForwardAdd" }
            Blend One One
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _Color;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wn : TEXCOORD0;
                fixed4 col : COLOR;
                float3 wp : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.col = v.color * _Color;
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.wn);
                float3 l = _WorldSpaceLightPos0.w == 0 ? _WorldSpaceLightPos0.xyz : normalize(_WorldSpaceLightPos0.xyz - i.wp);
                UNITY_LIGHT_ATTENUATION(atten, 0, i.wp);
                float d = smoothstep(-0.05, 0.5, dot(n, l)) * atten;
                fixed4 outc = fixed4(i.col.rgb * _LightColor0.rgb * d, 0);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, outc, fixed4(0,0,0,0));
                return outc;
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"
            struct v2f { V2F_SHADOW_CASTER; };
            v2f vert (appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }
            float4 frag (v2f i) : SV_Target { SHADOW_CASTER_FRAGMENT(i) }
            ENDCG
        }
    }
    Fallback "Diffuse"
}

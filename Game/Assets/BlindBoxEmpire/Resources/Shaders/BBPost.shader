// Small post stack: bloom (bright pass + separable blur) and a final grade (exposure, contrast, saturation, warmth, vignette).
Shader "Hidden/BB/Post"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float _Threshold;
    float _BloomIntensity;
    float4 _BlurDir;
    float _Exposure;
    float _Contrast;
    float _Saturation;
    float4 _Tint;
    float _Vignette;

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

    fixed4 fragBright (v2f i) : SV_Target
    {
        float2 t = _MainTex_TexelSize.xy;
        float3 c = tex2D(_MainTex, i.uv + t * float2(-0.5, -0.5)).rgb + tex2D(_MainTex, i.uv + t * float2(0.5, -0.5)).rgb
                 + tex2D(_MainTex, i.uv + t * float2(-0.5, 0.5)).rgb + tex2D(_MainTex, i.uv + t * float2(0.5, 0.5)).rgb;
        c *= 0.25;
        float br = max(c.r, max(c.g, c.b));
        float k = saturate((br - _Threshold) / max(0.0001, 1 - _Threshold));
        return fixed4(c * k, 1);
    }

    fixed4 fragBlur (v2f i) : SV_Target
    {
        float2 d = _BlurDir.xy * _MainTex_TexelSize.xy;
        float3 c = tex2D(_MainTex, i.uv).rgb * 0.227;
        c += tex2D(_MainTex, i.uv + d * 1.38).rgb * 0.316;
        c += tex2D(_MainTex, i.uv - d * 1.38).rgb * 0.316;
        c += tex2D(_MainTex, i.uv + d * 3.23).rgb * 0.070;
        c += tex2D(_MainTex, i.uv - d * 3.23).rgb * 0.070;
        return fixed4(c, 1);
    }

    fixed4 fragFinal (v2f i) : SV_Target
    {
        float3 c = tex2D(_MainTex, i.uv).rgb;
        c += tex2D(_BloomTex, i.uv).rgb * _BloomIntensity;
        c *= _Exposure;
        c *= _Tint.rgb;
        // soft filmic-ish shoulder
        c = c / (1 + c * 0.18) * 1.18;
        float l = dot(c, float3(0.2126, 0.7152, 0.0722));
        c = lerp(float3(l, l, l), c, _Saturation);
        c = (c - 0.5) * _Contrast + 0.5;
        float2 q = i.uv - 0.5;
        c *= 1 - dot(q, q) * _Vignette * 2.2;
        return fixed4(saturate(c), 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
               #pragma vertex vert
               #pragma fragment fragBright
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert
               #pragma fragment fragBlur
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert
               #pragma fragment fragFinal
               ENDCG }
    }
}

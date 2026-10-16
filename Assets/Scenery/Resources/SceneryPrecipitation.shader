Shader "Train/Scenery Precipitation"
{
    Properties { _Rain ("Rain streak", Float) = 0 _UseSceneLighting ("Scene lighting", Float) = 1 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "TrainSceneryLighting.cginc"
            float _Rain;
            struct Attributes { float4 position:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 position:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; UNITY_FOG_COORDS(2) };
            Varyings vert(Attributes v)
            {
                Varyings o; o.position=UnityObjectToClipPos(v.position);o.world=mul(unity_ObjectToWorld,v.position).xyz;o.color=v.color;o.uv=v.uv;
                UNITY_TRANSFER_FOG(o,o.position);return o;
            }
            fixed4 frag(Varyings i):SV_Target
            {
                float2 p=i.uv*2-1;
                float shape=lerp(saturate(1-dot(p,p)),saturate(1-abs(p.x))*saturate(1-abs(p.y)),_Rain);
                float3 illumination=TrainBeamLight(i.world,float3(0,1,0),0);
                fixed4 color=fixed4(i.color.rgb*(illumination*1.8+float3(.035,.055,.08)),i.color.a*shape);
                UNITY_APPLY_FOG(i.fogCoord,color);return color;
            }
            ENDCG
        }
    }
}

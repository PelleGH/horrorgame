Shader "Train/Scenery Vertex Color"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _UseSceneLighting ("Use carriage lights", Float) = 0
        _TrainSceneryAmbient ("Night ambient", Color) = (.03, .05, .08, 1)
        _FoliageWindStrength ("Foliage breeze", Float) = .16
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100
        Pass
        {
            // An untagged pass is Always in the built-in renderer and
            // SRPDefaultUnlit in URP, so both pipelines can draw this shader.
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma target 3.0
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "TrainSceneryLighting.cginc"

            fixed4 _Color;
            float _FoliageWindStrength;

            struct Attributes
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                float2 wind : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float3 world : TEXCOORD0;
                float3 normal : TEXCOORD1;
                UNITY_FOG_COORDS(2)
                float3 surface : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 world = mul(unity_ObjectToWorld,input.position).xyz;
                float phase = input.position.x * .37 + input.position.z * .53;
                world.x += sin(phase+_Time.y*1.4)*input.wind.x*_FoliageWindStrength;
                world.z += cos(phase+_Time.y*.95)*input.wind.x*_FoliageWindStrength*.35;
                output.position = mul(UNITY_MATRIX_VP,float4(world,1));
                float3 normal = UnityObjectToWorldNormal(input.normal);
                // Stable, inexpensive modelling light for generated exterior geometry.
                // Imported prefabs retain their own shaders and scene lighting.
                fixed4 vertexColor = input.color.a > 0 ? input.color : fixed4(1, 1, 1, 1);
                output.color = fixed4(vertexColor.rgb * _Color.rgb, 1);
                output.world = world;
                output.surface = input.position.xyz;
                output.normal = normal;
                UNITY_TRANSFER_FOG(output, output.position);
                return output;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                fixed4 color = input.color;
                float3 n = normalize(input.normal);
                float daylight = .45 + .55 * saturate(dot(n, normalize(float3(-.4,.8,.3))));
                color.rgb *= lerp(daylight.xxx, TrainBeamLight(input.world,n,1), _UseSceneLighting);
                float grain = frac(sin(dot(floor(input.surface.xz*18),float2(12.9898,78.233)))*43758.5453);
                float mottling = sin(input.surface.x*1.7+sin(input.surface.z*.83))*sin(input.surface.z*1.13);
                color.rgb *= .93 + grain*.1 + mottling*.055;
                UNITY_APPLY_FOG(input.fogCoord, color);
                return color;
            }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}

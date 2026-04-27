Shader "Custom/AsciiPost"
{
	Properties
	{
		_MainTex("Source", 2D) = "white" {}
		_GlyphAtlas("Glyph Atlas", 2D) = "white" {}
		_CellSize("Cell Size", Vector) = (8, 12, 0, 0)
		_GlyphCount("Glyph Count", Float) = 10
		_Tint("Tint", Color) = (1,1,1,1)
		_UseColor("Use Source Color", Float) = 1
		_TargetColumns("Target Columns", Float) = 160
	}

		SubShader
		{
			Tags { "RenderPipeline" = "UniversalPipeline" }

			Pass
			{
				Name "AsciiPostPass"
				ZWrite Off
				ZTest Always
				Cull Off

				HLSLPROGRAM
				#pragma vertex Vert
				#pragma fragment Frag

				#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

				TEXTURE2D(_BlitTexture);
				SAMPLER(sampler_BlitTexture);

				TEXTURE2D(_GlyphAtlas);
				SAMPLER(sampler_GlyphAtlas);

				float4 _CellSize;
				float _GlyphCount;
				float4 _Tint;
				float _UseColor;
				float _TargetColumns;

				struct Attributes
				{
					uint vertexID : SV_VertexID;
				};

				struct Varyings
				{
					float4 positionCS : SV_POSITION;
					float2 uv : TEXCOORD0;
				};

				Varyings Vert(Attributes input)
				{
					Varyings output;

					float2 pos;
					if (input.vertexID == 0) pos = float2(-1.0, -1.0);
					else if (input.vertexID == 1) pos = float2(-1.0, 3.0);
					else pos = float2(3.0, -1.0);

					output.positionCS = float4(pos, 0.0, 1.0);
					output.uv = float2(
						(pos.x + 1.0) * 0.5,
						1.0 - ((pos.y + 1.0) * 0.5)
						);

					return output;
				}

				float Luminance(float3 c)
				{
					return dot(c, float3(0.2126, 0.7152, 0.0722));
				}

				half4 Frag(Varyings input) : SV_Target
				{
					float2 screenSize = _ScreenParams.xy;
					float2 pixel = input.uv * screenSize;

					float screenWidth = _ScreenParams.x;
					float cellWidth = screenWidth / _TargetColumns;

					// maintain aspect ratio (characters taller than wide)
					float cellHeight = cellWidth * 1.5;

					float2 cellSize = float2(cellWidth, cellHeight);
					float2 cellCoord = floor(pixel / cellSize);
					float2 localCoord = floor(frac(pixel / cellSize) * cellSize) / cellSize;

					float2 cellCenterPx = (cellCoord + 0.5) * cellSize;
					float2 cellCenterUV = cellCenterPx / screenSize;

					float2 texel = 1.0 / screenSize;

					float3 sourceColor =
						SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, cellCenterUV).rgb +
						SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, cellCenterUV + texel).rgb +
						SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, cellCenterUV - texel).rgb;

					sourceColor /= 3.0;

					// Retro-ish colors
					sourceColor = floor(sourceColor * 6.0) / 6.0;

					//float lum = saturate(Luminance(sourceColor));
					//lum = saturate((lum - 0.5) * 1.4 + 0.5);
					//int glyphIndex = min((int)floor(lum * _GlyphCount), (int)_GlyphCount - 1);

					// SHARPER CONTRAST
					float lum = saturate(Luminance(sourceColor));
					lum = pow(lum, 0.6);
					int glyphIndex = min((int)floor(lum * _GlyphCount), (int)_GlyphCount - 1);

					float2 glyphUV = localCoord;
					glyphUV.x = (glyphUV.x + glyphIndex) / _GlyphCount;

					float glyph = SAMPLE_TEXTURE2D(_GlyphAtlas, sampler_GlyphAtlas, glyphUV).r;

					float3 baseColor = lerp(_Tint.rgb, sourceColor, _UseColor);
					float3 outColor = baseColor * glyph;

					return half4(outColor, 1.0);
				}
				ENDHLSL
			}
		}
}
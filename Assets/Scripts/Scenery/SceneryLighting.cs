using System.Collections.Generic;
using UnityEngine;

/// <summary>Small shared lighting contract for generated scenery and precipitation.</summary>
public static class SceneryLighting
{
    public const int MaximumBeams = 8;
    public static void Apply(Material material, Color ambient, IReadOnlyList<Light> beams,
        Vector4[] positions, Vector4[] directions, Vector4[] colors)
    {
        if (material == null) return;
        int count = 0;
        if (beams != null) for (int i = 0; i < beams.Count && count < MaximumBeams; i++)
        {
            Light light = beams[i];
            if (light == null || !light.isActiveAndEnabled || light.intensity <= 0) continue;
            Vector3 p = light.transform.position, d = light.transform.forward;
            positions[count] = new Vector4(p.x, p.y, p.z, Mathf.Max(.1f, light.range));
            directions[count] = new Vector4(d.x, d.y, d.z, light.type == LightType.Point ? -1f : Mathf.Cos(light.spotAngle * .5f * Mathf.Deg2Rad));
            Color color = light.color * light.intensity * .16f;
            colors[count] = new Vector4(color.r, color.g, color.b, 1); count++;
        }
        material.SetFloat("_UseSceneLighting", 1);
        material.SetColor("_TrainSceneryAmbient", ambient);
        material.SetInt("_TrainSceneryBeamCount", count);
        material.SetVectorArray("_TrainSceneryBeamPositions", positions);
        material.SetVectorArray("_TrainSceneryBeamDirections", directions);
        material.SetVectorArray("_TrainSceneryBeamColors", colors);
    }
}

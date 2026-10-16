using System;
using UnityEngine;

/// <summary>Authoring controls for the demonstration coach. Dimensions are interior metres.</summary>
[Serializable]
public class SceneryCarriageSettings
{
    [Header("Coach dimensions / forward is +Z")]
    [Min(3f)] public float interiorWidth = 4.8f;
    [Min(12f)] public float length = 23.5f;
    [Min(2.7f)] public float ceilingHeight = 3.35f;
    [Min(.8f)] public float deckHeight = 1.2f;
    [Range(1, 8)] public int compartmentCount = 3;
    [Min(.7f)] public float vestibuleLength = 1.2f;
    [Min(.9f)] public float aisleWidth = 1.3f;
    [Min(.9f)] public float doorwayWidth = 1.25f;

    [Header("Forward-facing seating")]
    [Range(1, 3)] public int seatsPerSide = 2;
    [Min(1f)] public float seatPitch = 1.45f;
    [Min(.45f)] public float seatWidth = .78f;
    [Min(.45f)] public float seatDepth = .76f;
    [Min(.6f)] public float rowEndClearance = 1.05f;
    [Tooltip("Zero fills each compartment automatically using Seat Pitch. A positive value limits its rows.")]
    [Min(0)] public int maximumRowsPerCompartment;
    [Tooltip("Optional seat model, with its feet at local Y=0 and facing +Z. It must fit the configured width and depth. Its authored scale and colliders are preserved.")]
    public GameObject seatPrefab;

    [Header("Windows / period details")]
    [Range(2, 5)] public int windowsPerCompartment = 3;
    [Min(.75f)] public float windowSillHeight = .98f;
    [Min(1.8f)] public float windowTopHeight = 2.62f;
    public bool curtains = true;
    public bool luggageRacks = true;
    public bool radiators = true;
    public bool floorboardDetails = true;
    [Tooltip("Combine compatible static coach renderers at startup. Disable when a custom seat prefab needs to move independently.")]
    public bool batchStaticGeometry = true;
    [Tooltip("Yaw at spawn; a slight turn reveals the windows as well as the aisle.")]
    [Range(-80f, 80f)] public float startingViewYaw = -24f;

    [Header("Palette / optional material overrides")]
    public Color floorColor = new Color(.155f, .105f, .073f);
    public Color wallColor = new Color(.52f, .51f, .42f);
    public Color woodColor = new Color(.245f, .15f, .085f);
    public Color seatColor = new Color(.12f, .225f, .19f);
    public Color trimColor = new Color(.42f, .36f, .23f);
    public Color shellColor = new Color(.095f, .155f, .145f);
    public Color curtainColor = new Color(.27f, .19f, .12f);
    public Material floorMaterial, wallMaterial, woodMaterial, seatMaterial;
    public Material trimMaterial, shellMaterial, curtainMaterial;

    public void Validate()
    {
        interiorWidth = Mathf.Clamp(interiorWidth, 3f, 8f);
        length = Mathf.Clamp(length, 12f, 45f);
        ceilingHeight = Mathf.Clamp(ceilingHeight, 2.7f, 5.5f);
        deckHeight = Mathf.Clamp(deckHeight, .8f, 2f);
        vestibuleLength = Mathf.Clamp(vestibuleLength, .7f, length * .1f);
        compartmentCount = Mathf.Clamp(compartmentCount, 1, Mathf.Min(8, Mathf.FloorToInt((length - vestibuleLength * 2) / 3.5f)));
        aisleWidth = Mathf.Clamp(aisleWidth, .9f, Mathf.Min(2f, interiorWidth - 1.6f));
        doorwayWidth = Mathf.Clamp(doorwayWidth, .9f, aisleWidth);
        seatsPerSide = Mathf.Clamp(seatsPerSide, 1, 3);
        seatWidth = Mathf.Clamp(seatWidth, .45f, 1.2f);
        seatDepth = Mathf.Clamp(seatDepth, .45f, 1f);
        seatPitch = Mathf.Clamp(seatPitch, seatDepth + .45f, 2.5f);
        rowEndClearance = Mathf.Clamp(rowEndClearance, .6f, 1.6f);
        maximumRowsPerCompartment = Mathf.Clamp(maximumRowsPerCompartment, 0, 24);
        windowsPerCompartment = Mathf.Clamp(windowsPerCompartment, 2, 5);
        windowSillHeight = Mathf.Clamp(windowSillHeight, .75f, 1.3f);
        windowTopHeight = Mathf.Clamp(windowTopHeight, windowSillHeight + .75f, ceilingHeight - .35f);
    }
}

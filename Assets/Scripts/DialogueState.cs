using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Remembers which dialogues have been played this session, so triggers can switch to a
/// "repeat" dialogue. Survives scene loads but not quitting the game; call GetPlayCount /
/// Reset from your save system if it needs to persist.
/// </summary>
public static class DialogueState
{
    private static readonly Dictionary<DialogueAsset, int> playCounts = new Dictionary<DialogueAsset, int>();

    // Keeps play mode clean when Domain Reload is disabled in the editor.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        playCounts.Clear();
    }

    public static int GetPlayCount(DialogueAsset asset)
    {
        if (asset == null)
            return 0;

        return playCounts.TryGetValue(asset, out int count) ? count : 0;
    }

    public static bool HasPlayed(DialogueAsset asset) => GetPlayCount(asset) > 0;

    public static void MarkPlayed(DialogueAsset asset)
    {
        if (asset == null)
            return;

        playCounts[asset] = GetPlayCount(asset) + 1;
    }

    public static void Reset()
    {
        playCounts.Clear();
    }
}

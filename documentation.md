# Dialogue System

Conversations with NPCs and objects: a text box with a typewriter effect, optional
branching choices, per-character styling, and a camera that turns toward the speaker.

> **Status:** written without running Unity. Compile and play-test before relying on it.

## Quick start

**Give an object dialogue**

1. Create a dialogue: right-click in the Project window → **Create → Dialogue → Dialogue Asset**, then add lines.
2. Add a **DialogueTrigger** component to the object and drag the asset into **Dialogue**.
3. Put the object's collider on the **interactLayer** layer (the layer `PlayerInteract` raycasts against).
4. Play, look at the object within 3 units, press **E**.

No scene setup is needed. `DialogueManager` creates itself and builds its own UI the first time it is used.

A working example is **DialogueTestObject** in `Assets/Scenes/trainstation.unity`, using the assets in `Assets/Dialogue/`.

**Controls**

| Action | Keys |
|---|---|
| Skip typing / next line | E, Space or left click |
| Move between choices | W / S, Up / Down, mouse scroll |
| Pick a choice | E, Space, click, or number keys 1–9 |

## How it works

```
 PlayerInteract ──(E on looked-at object)──▶ DialogueTrigger.Interact()
                                                   │ picks asset (dialogue or repeatDialogue)
                                                   ▼
                                       DialogueManager.StartDialogue(asset, ...)
                                                   │ freezes player, shows UI, plays lines
                                                   ▼
        line ─▶ line ─▶ ... ─▶ choices ─▶ chosen asset ─▶ ... ─▶ end
                                                   │
                                                   ▼
                       unfreezes player, fires onFinished / OnDialogueEnded
```

Only one dialogue can run at a time. `StartDialogue` returns `false` if one is already open.

## Files

| File | Role |
|---|---|
| `DialogueAsset.cs` | ScriptableObject holding the content: lines, choices, optional `next`. |
| `dialouge.cs` | Plain data classes `DialogueLine` and `DialogueChoice` (the filename keeps its original spelling). |
| `SpeakerProfile.cs` | ScriptableObject for a character's look and sound. |
| `DialogueTrigger.cs` | Component on the world object. Starts dialogue, handles highlight and events. |
| `DialogueManager.cs` | Singleton that plays dialogue. Owns the UI, input, typewriter, choices, camera focus. |
| `DialogueState.cs` | Static record of which assets have been played this session. |

Existing scripts touched:

- `PlayerInteract.cs`: finds the `DialogueTrigger` you are looking at, highlights it, shows the prompt, starts it on E.
- `ItemInspector.cs`: ignores E while `DialogueManager.IsBusy`.
- `Player.cs` (`SimpleFPSControllerNewInput`): new public `Pitch` property so camera focus does not snap back afterwards.

## Content: `DialogueAsset`

| Field | Meaning |
|---|---|
| `lines` | Shown in order. |
| `choices` | Optional. Shown after the last line. Each has `text` and a `next` asset. Empty `next` ends the conversation. |
| `next` | Played automatically after this asset when it has no choices. |

Each **line** (`DialogueLine`) has:

| Field | Meaning |
|---|---|
| `speaker` | Name shown above the text. If empty, the profile's `displayName` is used. If both are empty, no name is shown. |
| `text` | The line. TextMeshPro rich text works (`<i>`, `<color=#8a4a46>`). |
| `profile` | Optional `SpeakerProfile`. |
| `voice` | Optional clip played when the line starts. |

**Branching** is just assets pointing at other assets. Loops are allowed (a choice can point back to an earlier asset).

```
StrangerIntro ──choice "Where is the train?"──▶ StrangerTrain ──▶ end
              ├─choice "Who are you?" ─────────▶ StrangerWho   ──▶ end
              └─choice "Leave."  ───────────────▶ end
```

### `SpeakerProfile`

Create via **Create → Dialogue → Speaker Profile**. Sets `displayName`, `nameColor`, `textColor`,
`speedMultiplier` (below 1 = slower typing), and an optional `blipClip` with volume and pitch range
that overrides the manager's default blip.

## The trigger: `DialogueTrigger`

| Field | Meaning |
|---|---|
| `dialogue` | Asset played the first time. |
| `repeatDialogue` | Optional. Played instead of `dialogue` once `dialogue` has been played before. |
| `playOnce` | Can only start once per scene load. Overrides `repeatDialogue`. |
| `triggerOnEnter` | Also starts when the player walks into this object's trigger collider. |
| `promptText` | Text after the key in the prompt, e.g. `[E] talk`. |
| `focusCamera`, `focusPoint` | Turn the view toward the speaker. `focusPoint` empty = centre of the collider. |
| `highlightIntensity`, `highlightSaturationBoost`, `highlightRenderers` | Glow settings (see below). |
| `onStarted`, `onFinished` | UnityEvents for the whole conversation. |
| `onLineStarted(int)` | Fires for every line shown, with the line's index. |
| `lineEvents` | Fire an event for one specific line index (e.g. flicker a light on line 2). |

Line indexes are relative to the asset being played. `lineEvents` only fire for the asset the trigger started,
not for assets reached through choices. `onLineStarted(int)` fires for those too.

## Systems worth knowing

**Player freeze.** While a dialogue is open, `DialogueManager` sets `playerController.isInspecting = true`,
the same flag `ItemInspector` uses, so movement and look stop. It is released when the dialogue ends.

**Input guards.** Several scripts use E. `DialogueManager.IsBusy` is true while a dialogue is open *and*
for the frame it closed, so the press that closes it cannot also open a door or an inspected item.
Any new E-key interaction should check `DialogueManager.IsBusy` first.

**Repeat visits (`DialogueState`).** Every time an asset starts, its play count goes up. State is per asset,
so two NPCs sharing one asset share its played flag. It lasts for the session but is not saved to disk
and is cleared when entering play mode. For saving, read `GetPlayCount` and restore with `MarkPlayed`.

**Camera focus.** Each frame the manager smoothly turns the player root (yaw) and camera (pitch) toward the
focus point. The view is not restored afterwards. Smoothing is `focusSmoothTime`.

**Interact prompt and glow.** `PlayerInteract` calls `SetHighlight` and `ShowInteractPrompt` when you look at
a trigger, and clears them when you look away or a dialogue opens. The glow is derived from the object's
material: coloured materials get a more saturated glow, greys a lighter grey, textured materials glow in
their own texture. It only works on materials with an `_EmissionColor` property (URP Lit, for example). Others
are skipped and just get the prompt.

**UI.** Built in code at runtime (a Screen Space Overlay canvas, sort order 100) and styled to be dim and
low-contrast: grey text, faint box, 0.3 s fade. Every colour, size and the fade time is a public field on
`DialogueManager`. Text uses TextMeshPro's default font (Liberation Sans).

## Scripting API

```csharp
// Start from code. Returns false if busy or the asset is empty.
DialogueManager.Instance.StartDialogue(
    asset,
    focus: () => npc.position,                 // optional: camera target, null = no camera move
    onFinished: () => Debug.Log("done"),       // optional
    onLineStarted: (a, i) => { });             // optional: asset and line index

DialogueManager.IsActive       // dialogue open
DialogueManager.IsBusy         // open, or closed this frame (use for input guards)
DialogueManager.OnDialogueStarted / OnDialogueEnded   // static events

DialogueState.HasPlayed(asset) / GetPlayCount(asset) / MarkPlayed(asset) / Reset()
```

## Common tasks

- **New NPC, same conversation:** add a `DialogueTrigger` and point it at the existing asset.
- **NPC says something different on the second visit:** fill in `repeatDialogue`.
- **Trigger something mid-conversation:** add a `lineEvents` entry with the line index and wire up the UnityEvent.
- **Different voice per character:** make a `SpeakerProfile`, assign it on that character's lines.
- **Auto-start when entering a room:** add a trigger collider, tick `triggerOnEnter`.
- **Retune the look:** edit the public fields on `DialogueManager` (put one in the scene, or edit the defaults in code).

## Limitations

- One dialogue at a time; no queueing.
- Uses the legacy `Input` API, like `PlayerInteract` and `ItemInspector`. The keys are not rebindable.
- No save integration and no localization.
- No monospace or custom font is set up, so text does not match the ASCII renderer's glyphs.
- Choices cannot be conditional (every choice always shows).

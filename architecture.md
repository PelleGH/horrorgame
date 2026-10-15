# Game Architecture

Quick reference for developers implementing gameplay features.

This document explains how to **use and extend existing systems**. Keep instructions short and update this document when new systems are introduced.

## 1. Interaction System

**Scripts:** `PlayerInteract.cs`, `IInteractable.cs`

The player uses a single raycast to detect interactable objects. Pressing **E** calls the object's interaction behavior.

### Making an object interactable

1. Add a Collider to the object (or a child).
2. Make sure its layer is included in the player's `Raycast Mask`.
3. Create a script implementing `IInteractable`.
4. Attach the script to the object or a parent of the Collider.

Example:

```csharp
using UnityEngine;

public class MyInteractable : MonoBehaviour, IInteractable
{
    public bool CanInteract => true;
    public string InteractionPrompt => "use";

    public void Interact(PlayerInteract player)
    {
        Debug.Log("Interacted!");
    }

    public void SetHighlight(bool highlighted)
    {
        // Optional highlight effect
    }
}
```

The player will now see **[E] use** when looking at the object.

**Important:** Do not add individual raycasts or interaction-key listeners to new objects. The player handles interaction detection automatically.

## 2. Inspectable Items

**Scripts:** `InspectableItem.cs`, `ItemInspector.cs`

Allows players to pick up, rotate, zoom, and examine objects.

### Making an item inspectable

1. Add a Collider.
2. Add the `InspectableItem` component.
3. Set the object's layer to one included in the player's Raycast Mask.
4. Configure inspection distance, scale, and rotation speed in the Inspector.

The player's `ItemInspector` must have its camera and player controller assigned, and `PlayerInteract` must reference that `ItemInspector`.

**Controls:**

- E — Pick up / put down
- Left Mouse + drag — Rotate
- Scroll — Zoom
- R — Reset rotation
- Escape — Put down

No additional scripts are needed for ordinary inspectable objects.

## 3. Doors

**Script:** `Door.cs`

### Adding a door

1. Create a door GameObject with a Collider.
2. Add the `Door` component.
3. Assign `Door Root` to the rotating hinge transform.
4. Assign `Handle Renderer` for highlighting.
5. Configure `Open Angle` and `Speed`.
6. Ensure the Collider's layer is included in the player interaction raycast mask.

The door automatically supports E interaction and highlighting.

For sound, use **Tools > Horror Audio > Add Sound...**, select **Door**, and
configure Opening, Closing and Latched on `DoorAudio`. `Door` owns motion and
events; `DoorAudio` owns its audio responses. The latch follows the actual closed
position. See [Audio documentation](audiodocumentation.md#door-audio-sequences-and-triggers).

## 4. Dialogue System

**Scripts:** `DialogueTrigger.cs`, `DialogueManager.cs`, `DialogueAsset.cs`, `SpeakerProfile.cs`

### Creating dialogue

1. Right-click in Project → **Create → Dialogue → Dialogue Asset**.
2. Add dialogue lines and optional choices.
3. Optionally create a Speaker Profile for character-specific styling and sounds.
4. Add `DialogueTrigger` to an NPC or object.
5. Assign the Dialogue Asset.
6. Add a Collider on an included interaction layer.

The player can now look at the NPC and press **E** to start talking.

### Optional settings

- `Repeat Dialogue` — Alternative conversation after the first.
- `Play Once` — Only allows the conversation once per scene load.
- `Trigger On Enter` — Starts dialogue automatically when entering its trigger.
- `Focus Camera` — Turns the player's view toward the speaker.
- `Line Events` — Trigger UnityEvents at specific dialogue lines.

For automatic dialogue, configure an appropriate trigger Collider.

The `DialogueManager` manages the UI and conversation flow automatically.

## 5. Player Controller

**Script:** `Player.cs` (`SimpleFPSControllerNewInput`)

Handles movement, looking, jumping, and crouching. It exposes movement state;
`PlayerFootstepAudio` handles footstep timing and playback through `SoundEmitter`.

The `isInspecting` field is currently used to suspend player movement and camera look during inspection and dialogue.

When implementing new gameplay modes that take control away from the player, ensure they coordinate with existing interaction states.

**Input:** The project currently uses both Unity's legacy and new Input System APIs. Set Active Input Handling to `Both`.

---

## 6. Audio System

**Namespace and location:** `HorrorAudio`, `Assets/Scripts/Audio`.

Gameplay components report events or movement state. Audio adapters select sounds
and call `SoundEmitter`; `SoundEngine` manages the listener, shared voice capacity,
priority and environment state.

| Component | Responsibility |
| --- | --- |
| `SoundEmitter` | Clips, layers, playback, EQ/effects, 3D settings and voice reuse |
| `PlayerFootstepAudio` | Walk/sprint/crouch cadence and floor sound selection |
| `DoorAudio` | Opening, closing and latch responses to door events |
| `SoundImpact` | Collision sounds based on speed and surface |
| `SoundSequencePlayer` / `SoundTrigger` | Timed sequences, trigger zones, UnityEvents and animation events |
| `WorldSoundEmitter` | Continuous global or positional ambience with fades |
| `SoundEnvironmentZone` / `SoundAcousticMaterial` | Room treatment and approximate obstruction/transmission |

**Setup:** select the object and use **Tools > Horror Audio > Add Sound...**.
Assign a direct clip, or create a **Horror Audio > Sound Profile** or **Sound
Sequence** asset. Editor setup and previews remain available in development;
their code is in the `Editor` directory.

**Extension:** use `PlayDefault()`, `PlayClip(...)`, `Play(profile, ...)` and
`StopAll()` on the emitter. Add an event adapter for new gameplay behaviour.
Dialogues already route recorded lines and generated syllables through emitters.
For older AudioSources, use **Import Existing Audio In Open Scenes**.

**Conventions:** lower numeric priority is more important; -1 inherits the source
or profile priority. Direct clips use emitter effects; reusable profiles own their
effects. Room treatment and obstruction are approximations. Production capacity
must be profiled with the game's real clips and scenes.

Full setup, sound creation, APIs and optimization details:
[Audio documentation](audiodocumentation.md).

## Adding New Systems

When implementing a new gameplay system, add a short section covering:

**System Name**

- **Scripts:** Relevant scripts
- **Purpose:** What the system does
- **How to use:** Steps to configure it in Unity
- **How to extend:** How to add functionality, if relevant
- **Important:** Any limitations or conventions developers should know

Keep sections focused on practical usage rather than internal implementation details.

For complex systems, link to separate documentation instead of expanding this file indefinitely.

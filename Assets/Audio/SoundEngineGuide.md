# Adaptive Sound Engine

This system uses Unity's audio playback and effects, with reusable profiles,
layered sounds, generated samples and optional environment adaptation. Scripts
are in `Assets/Scripts/Audio`, in the `HorrorAudio` namespace.

## Try it immediately

Open `Assets/Scenes/AudioTest.unity`, or **Tools > Horror Audio > Open Audio Test
Scene**, then press Play. The scene generates its own player and geometry.

- WASD: walk; Shift: sprint; Ctrl: crouch.
- F: open/close the test door.
- B: drop a ball to test collision sounds.
- Walk onto the right-hand metal floor: footsteps change, and the environment
  zone adds reverb and filtering.
- Up/Down arrows: change simulated train speed and hear the motor respond.

No imported clips are needed. The starter sounds are deliberately simple
procedural examples, not finished recordings or realistic Foley.

## Add audio to your game

### Direct audio files: no profile asset required

1. Import your sound file into Assets (for example WAV or OGG).
2. Add **Horror Audio > Sound Emitter** to the object.
3. Drag the imported AudioClip into **Audio Clip**. Add **Clip Variations** if needed.
4. Expand **Sound Settings** to adjust looping, volume, pitch variation, 3D distance,
   mixer routing, bass, treble, distortion, filters, reverb and echo.
5. Enable **Play On Enable** for ambience, or call **PlayDefault()** from a
   UnityEvent, animation event or script. In Play, use the Inspector preview buttons.

**Extra Layers** can add recorded or generated texture to the file. These use the
same environment, train and occlusion settings as the primary audio. **Save Object
Sound As Reusable Profile** exports the configuration as a project asset; it does
not modify or export the underlying audio file.

Direct files override Default Profile for default playback. Explicit Play(profile)
still uses that profile. Object Sound Settings affect the direct files, while an
external profile owns its own effects. The outer emitter Volume/Pitch multiply
both kinds of sound. Changing the clip/variations requires a new playback; effects
and volume update while playing.

Door has optional **Open Clip**, **Close Clip** and **Latch Clip** fields, and
SoundImpact has an **Impact Clip** field. These use the owning emitter's Sound
Settings. If no action clip is assigned, a direct emitter file is used for door
movement and object impacts before the starter profiles. Footsteps use the direct
file when the floor does not explicitly supply a SoundSurface footstep override.
Door, impact and character events are one-shot even if the emitter's ambience
setting has Loop enabled. A surface's explicit profile takes precedence for
footsteps and impacts.

### Automatic integration helpers

Select the GameObject containing your player controller, Door, or Rigidbody.
Use **Tools > Horror Audio > Set Up Selected Objects**. This supports multiple
selected objects and Undo, and records prefab instance overrides. Running it again
preserves already assigned profiles and audio files.

- **Player:** adds/reuses SoundEmitter and assigns the Footsteps profile. Your
  existing movement, crouch and step timing still trigger the sounds. Old
  AudioSource/AudioClip footsteps remain the fallback when no profile is assigned.
- **Door:** assigns opening, closing and latch profiles. Opening/closing sound
  starts with movement; the latch plays when rotation reaches the target.
- **Rigidbody object:** adds SoundImpact and assigns an impact profile.
- **Other object:** adds SoundEmitter with an Impact default profile. Trigger
  PlayDefault from your own code, an animation event or UnityEvent.

Use prefab mode to apply the configuration to a reusable character or object.
The menu configures the selected object, not every descendant. Existing scene
objects and prefabs are not automatically rewritten when the scripts import.

## Create and customize sounds

Right-click in Project > **Create > Horror Audio > Sound Profile**. Assign the
asset to an emitter, a player footstep field, a door sound field or SoundImpact.

Each profile contains independently pitched layers. A layer can contain imported
clip variations, or use **Generator** when its Clips array is empty:

| Generator | Intended use |
| --- | --- |
| None | Recorded clips only; an empty layer is silent |
| Impact | Short noise/tone transient for test footsteps and hits |
| Wind | Filtered noise for wind and air |
| Hum | Fundamental and harmonic for machines |
| Creak | Modulated tone/noise for stylized mechanical movement |

Layer controls: volume, pitch range, generated duration, frequency, noise and
seed. Available clip variations avoid immediately repeating the previous choice
where possible. Imported clips take precedence over synthesis. Use mono clips
for predictable positional sound.

Profile controls: overall volume, looping, 2D/3D blend, distance falloff, native
Doppler, Unity voice priority, and optional AudioMixerGroup output.

Distortion, low-pass, high-pass, reverb and echo can be adjusted while playing.
**Bass Db** boosts/cuts frequencies below roughly 250 Hz; **Treble Db** adjusts
frequencies above roughly 4 kHz. Both range from -12 to +12 dB. Zero is neutral.
These are real shelving EQ filters, not changes to playback pitch. Strong boosts
use a soft output ceiling; leave mixing headroom for layered sounds.
Profile/layer volume and emitter volume/pitch also update on active voices. Changes
to layer clips/pitch, routing and distance settings take effect on the next playback. Generated
buffers are cached: restart Play after changing synthesis settings. WarmUp() can
pre-generate a profile before an important event.

Layer volumes add together: leave headroom when adding more layers. Use the
Play Default Profile / Stop Sounds Inspector buttons during Play to audition.
Loop requests for the same profile on the same emitter reuse the existing loop.
StopAll stops all layers belonging to that emitter.

## Footsteps and surfaces

Add SoundSurface to floor colliders, or their parent. Assign Footsteps to override
the player's default profile. Assign Impacts to change object collision sounds
against that material. No tags or fixed material names are required.

On the player, Sprint Footstep Gain and Crouch Footstep Gain change loudness;
the existing walk/sprint/crouch intervals still control cadence. Footstep Surface
Mask must include the floor layer. The player's own layer is excluded from the
ground ray. A layer of gravel/noise can provide crunch without distorting the
entire footstep.

## Object impacts

Put SoundImpact on the GameObject receiving Rigidbody collision callbacks.
It needs a solid collider and normal Unity physics collision configuration.
Relative collision speed controls volume; Minimum Speed and Cooldown prevent
tiny repeated contacts from becoming a stream of sounds. The contacted surface's
impact profile overrides the object's fallback profile.

This adapter does not create physics for an object or alter its movement. It can
be attached to a thrown ball, bottle, box or any future inventory object.

## Environment and occlusion

Add SoundEnvironmentZone to an empty object and size its BoxCollider to the room.
The box becomes a trigger; no Rigidbody is needed for zone detection. The active
AudioListener's position selects the zone. Higher Priority wins in overlaps;
equal-priority zones use registration order. Outside zones, profile settings apply.

Zones affect SoundEmitters with Use Environment enabled. Volume and low-pass
changes are smoothed. Reverb preset changes switch presets; this prototype does
not crossfade two separate reverberation tails. Zones are listening environments,
not a physical acoustic simulation of each room and doorway.

### Room size and estimated reverberation

Enable **Calculate From Room Size** on a SoundEnvironmentZone. The box's local
size multiplied by transform scale gives room dimensions in metres. Set the box
to the interior room, not the outer building. Volume and surface areas feed a
Sabine approximation: **RT60 ≈ 0.161 × volume / equivalent absorption area**.
Wall, floor and ceiling absorption determine the equivalent area. Higher
absorption gives a shorter decay; a larger room with equal material coefficients
usually gives a longer decay. Values are clamped to a practical 0.1–10 seconds.

The Inspector shows dimensions, volume and estimated RT60. **Measure Room From
Walls** casts along the room's three axes in both directions, fits the box between
the six hits, and reads SoundAcousticMaterial absorption where supplied. Put its
center in clear interior space and use Measurement Mask to exclude furniture and
characters. Measurement is manual, not a ray scan every audio frame. It fails
without six enclosing surfaces; existing settings remain unchanged on failure.

For calculated rooms, estimated decay and early reflection delay drive a custom
Unity reverb filter. Room Level, Reflections Level and Tail Level control the mix.
**Room Echo Wet** optionally adds distinct repeated echoes; its delay is estimated
from twice the largest room dimension divided by 343 m/s. Leave it at zero when
you want diffuse reverberation rather than a noticeable repeated echo.

These are gameplay approximations. Six rays do not reconstruct an irregular room,
furniture, an open window or connected spaces. A room's size alone cannot determine
its sound, and visible materials do not reveal acoustic absorption. Assign those
values yourself. Unity's reverb controls support custom decay/reflection settings;
the Sabine model is a statistical estimate with known limits.
[Unity AudioReverbFilter](https://docs.unity3d.com/cn/6000.0/ScriptReference/AudioReverbFilter.html),
[room acoustics model and limitations](https://pubs.aip.org/asa/jasa/article-pdf/155/3/1747/19706144/1747_1_10.0025162.pdf).

Enable Occlusion on an emitter to check for solid blockers between it and the
listener every 0.15 seconds. Occluded Volume and Occluded Low Pass control the
result. Ignore transparent/nonblocking geometry with Occlusion Mask. Source and
listener-body colliders and trigger volumes are excluded. A rotating Door's solid
collider naturally blocks or clears the ray; no door-specific acoustic code is
needed. This is a single-ray approximation, not sound diffraction.

### Thickness and transmission

Add **SoundAcousticMaterial** to a wall, door, object collider or its parent.
Absorption describes room reflections; transmission loss describes sound passing
through the object. They are separate settings.

With **Measure Thickness From Collider** enabled, forward and reverse ray hits
on the same closed collider estimate the material path length. BoxCollider works
well, including rotation and scale. Several blockers multiply transmission losses.
**Base Transmission Loss Db**, **Loss Db Per Metre**, **Transmission Low Pass** and
**Full Filtering Thickness** tune the result. Objects without acoustic metadata
still use the emitter's original Occluded Volume / Occluded Low Pass fallback.

For an open mesh, hollow object or simplified collider, disable automatic
measurement and set **Fallback Thickness** in metres. The Inspector can populate
it from the smallest collider dimension, which is only a starting estimate. A
bottle's outer diameter is not its glass-wall thickness. Geometry cannot tell us
density, wall layers, airtightness or real frequency-dependent transmission loss;
the attenuation model is artist-tunable rather than a calibrated physical model.

## Train simulation and Doppler

The engine has no dependency on a particular path or train controller:

```csharp
using HorrorAudio;

// Normalized speed, turn magnitude and braking intensity.
SoundEngine.Instance.SetTrainMotion(speed01, turn01, brake01);
```

Enable Respond To Train Motion on an emitter to map those values to Ride Volume
and Ride Pitch. Speed affects pitch; the greater of speed and stress affects
volume. The engine is created when the first emitter or zone enables. If you call
the API before that, create a root GameObject with SoundEngine first.

For genuinely moving 3D sources, use the profile's Doppler Level and Unity's
source/listener motion. Keep it at zero for sounds fixed relative to the player,
such as the motor heard from the same train.

For a stationary train simulation, external sounds can receive a simulated radial
relative speed, positive when approaching and negative when receding:

```csharp
externalSound.SetSimulatedRelativeSpeed(radialSpeedMetresPerSecond);
```

This approximates a moving source relative to a stationary listener using a
343 m/s speed of sound and bounded pitch scaling. Native Doppler is disabled on
that emitter to prevent applying it twice. It does not calculate relative speed
from your train path, move the sound source or simulate changing distance: your
train/world controller supplies those values. Turn off Use Simulated Doppler to
return to native Doppler.

## APIs and existing systems

```csharp
emitter.Play(profile, gain: 0.8f, pitchMultiplier: 1.0f);
emitter.PlayDefault();             // also available to UnityEvents
emitter.PlayClip(myAudioClip);      // one-shot, using this object's Sound Settings
emitter.SetIntensity(0.5f);       // live emitter volume multiplier
emitter.StopAll();
```

Play returns false for missing/empty profiles or insufficient voice capacity.
Playback uses separate AudioSources per layer, so changing a new sound's pitch
does not retune an earlier sound. Voice allocation is lazy and reused per emitter.
Max Voices limits each emitter; SoundEngine Max Active Voices limits simultaneous
engine playback globally. A layered request is rejected as a whole if it cannot
fit. The engine does not steal important sounds based on priority; Unity's own
Priority setting is still forwarded to each AudioSource.

The engine auto-creates one persistent root and reuses the scene's AudioListener.
Do not add extra listeners. Emitter loops stop when their owner disables; scene
objects clean up their own sources. Master Volume controls engine emitters only.

DialogueVoice keeps its existing syllable generation, filters, pitch planning
and timing. Its optional Output Mixer field can route it into a shared mixer.
Dialogue and ItemInspector retain ownership of player.isInspecting. The sound
engine does not take keyboard or mouse input outside the isolated test scene.

There are no pocket or train simulation scripts in this branch, so those have
not been rewritten. Use the public emitter/impact/train APIs when integrating
those systems into this branch. Existing dialogue AudioSources do not automatically
receive emitter environment effects; route them through a mixer if desired.

## Practical limits

Generated samples are finite, cached mono clips at 22,050 Hz, with up to three
variants per generated layer. Loop buffers use an overlap to soften the seam.
Generation runs on the main thread at first use or WarmUp; it is not continuous
audio-thread synthesis. Keep profiles modest and preload critical sounds.

Native Unity filters provide most DSP; the bass/treble component runs two
shelving filters with immutable coefficient updates and per-channel state. Its
audio callback uses no Unity API, locks or heap allocation and supports up to
eight channels. This is not an FMOD/Wwise replacement or
a full acoustic simulator. It includes no recording/export editor, streaming
procedural generator, custom bitcrusher, convolution reverb or automatic sound
authoring. Replace the test clips with authored audio where realism matters.

Commit scripts, profiles, the test scene, this guide and their .meta files together.

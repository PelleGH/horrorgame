# Horror Audio Sound Engine

This system uses Unity's audio playback and effects, with reusable profiles,
layered sounds, generated samples and optional environment adaptation. Scripts
are in `Assets/Scripts/Audio`, in the `HorrorAudio` namespace.

## Quick start

Select an object and open **Tools > Horror Audio > Add Sound...**. Assign an audio
file and choose its behaviour, or add a SoundEmitter and assign a reusable
profile. Use **Preview 2D** to audition it before entering Play Mode. The sections
below explain player, door, collision, trigger and world ambience integration.

## Add audio to your game

### Existing game audio is routed through Horror Audio

The Player prefab now contains a SoundEmitter with the same 32 footstep files
previously assigned by the testlevel scene. No audio files are regenerated or
replaced. Its clip variations can be edited directly in the SoundEmitter Inspector.
PlayerFootstepAudio owns footstep timing, pitch/volume ranges, surface detection and legacy clip compatibility. SimpleFPSControllerNewInput only reports movement state and contains no audio logic. Other legacy
players with an unconfigured emitter import their existing array at startup.

**Use Original Footstep Dynamics** retains the original walk/crouch pitch and
volume ranges. The existing walk, sprint and crouch timing is preserved in PlayerFootstepAudio.
Turn this option off to use the newer sprint/crouch gain controls instead. Explicit
SoundSurface overrides still take precedence. The old footstep AudioSource stops
and disables so it cannot play the same step twice; it remains a metadata reference
for custom rolloff curves and specialised settings.

Recorded dialogue lines and procedural DialogueVoice syllables now also play through
SoundEmitter. The syllable generator, contour, muffle, distortion compensation and
four-voice replacement policy are preserved. DialogueManager exposes Line Sound
Emitter; DialogueVoice exposes Sound Emitter. Both create dedicated child emitters
when references are empty, with unity gain and 2D sound to match the previous
playback. Their emitter controls and the engine master volume now affect dialogue.
Rooms remain off for dialogue by default; enable Use Environment if desired.

For older objects, use **Tools > Horror Audio > Import Existing Audio In Open Scenes**.
The tool copies assigned clips and source configuration, creates an
**Existing AudioSource Adapter**, and disables old output. It supports Undo and
does not save scenes automatically. Supported Play/Stop/PlayOneShot persistent
UnityEvents in the open scenes are redirected to the adapter. Custom scripts,
animation events or third-party systems calling AudioSource directly must instead
call the adapter or SoundEmitter; native AudioSource calls cannot be intercepted.
Clipless scripted sources are not guessed or replaced. Re-running import skips
already imported sources. Open prefab assets in Prefab Mode to configure them.

The adapter provides Play(), Stop() and PlayOneShot(AudioClip). Source volume,
pitch, loop, mixer output, distance and Doppler are copied. The original source
remains as Legacy Template for rolloff curves, pan, spread, mute, listener flags,
priority and spatializer settings. Enabled basic distortion, high/low-pass, echo
and reverb preset/level are copied where supported. Custom reverb parameters,
chorus and third-party DSP are not translated automatically.

### Always-on world ambience

Create **GameObject > Horror Audio > World Emitter**, or choose **World** in Add
Sound on a dedicated empty object. The creation menu supplies the generated Wind
profile as a starting sound. Replace it with your own Audio Clip or profile in
SoundEmitter. WorldSoundEmitter manages the existing emitter's playback:

- Starts automatically, loops all layers and retries once per second if voice
  capacity temporarily prevents playback.
- **Global Sound** is on by default: 2D sound heard throughout the world, without
  positional attenuation. Turn it off for a 3D source using the usual distances.
- Fade In / Fade Out provide smooth start/stop without changing the authored volume.
- **StartWorldSound() / StopWorldSound()** can be called by UnityEvents or triggers.
  Inspector Play/Stop also controls the world emitter and respects its fades.
- Disabling the component stops its sound immediately. Re-enabling starts it again.
- **Persist Across Scenes** optionally keeps a dedicated root object alive. Place
  it once in a bootstrap scene; the system does not deduplicate world emitters in
  subsequent scenes. A child object cannot enable persistence.

Bass, effects, layering, room adaptation and train controls remain in SoundEmitter.
Global sound ignores wall occlusion because it has no positional path; room effects
can still apply when Use Environment is enabled. Shared profile assets are not
modified to enforce looping or 2D/3D mode. Use a dedicated object, separate from
door, player or impact emitters, because the world component owns its playback.
For a clean continuous sound, provide a clip with a seamless loop.

### Add Sound workflow

Select an object and open **Tools > Horror Audio > Add Sound...**, or right-click
the object in the Hierarchy and choose **Horror Audio > Add Sound...**.

Assign a sound file, choose **Manual Event**, **Ambience**, **Footsteps**, **Door**
or **Collision**, then click **Add / Configure Sound**. The tool connects the
existing player/door or adds the collision adapter. Ambience starts on enable and
loops. Manual Event exposes PlayDefault for your own UnityEvents/animation events.
The tool does not add physics or a door gameplay script; it explains missing
requirements before changing the object. Open prefab assets in Prefab Mode first.

**Keep Current** preserves existing sound settings. Other starting settings are
**Neutral**, **Metal Impact**, **Soft Wind** and **Small Train Car**. These are
artist starting points, not measured material presets. Applying one replaces
Object Sound Settings while preserving mixer routing. The chosen trigger decides
whether playback loops. Setup supports one-step Undo and preserves existing
gameplay components and their other events.

The Inspector opens with audio and basic playback controls. Variations & Layers,
Tone & Effects, 3D Sound & Environment, Train Adaptation and Advanced are separate
foldouts. Diagnostics explain missing files, manual triggers, missing collision
physics, an unassigned door root, missing listeners during Play and rejected
playback due to voice limits. A selected emitter shows its maximum-distance disc
in the Scene view, plus a listener line when testing occlusion in Play.

### Preview while building

**Preview 2D** works without entering Play Mode and does not spawn an emitter,
listener or manager into your scene. It mixes clip layers, pitch, volume, bass,
treble, frequency filters and approximate distortion/echo/reverb into a temporary
mono preview of up to ten seconds. Stop releases it; preview also stops on Play
Mode transitions, script reload or editor exit. No source asset is edited.

This is an authoring audition, not the runtime acoustics: native Unity reverb and
distortion can differ, and room effects, occlusion, 3D position, Doppler and mixer
routing need Play Mode. Compressed/streaming or very large clips may require
Decompress On Load or a Play Mode audition. Preview uses Unity's internal editor
audio API and reports when it is unavailable on another Unity version.

In Play Mode, the same Inspector buttons play/stop the actual runtime emitter.

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

DoorAudio has independent Opening, Closing and Latched events. Each accepts a
Clip, Profile or Sequence. Direct clips use the owning emitter's Sound Settings.
SoundImpact has an Impact Clip field and falls back to a direct emitter sound
before its default profile. Footsteps use the direct
file when the floor does not explicitly supply a SoundSurface footstep override.
Door, impact and character events are one-shot even if the emitter's ambience
setting has Loop enabled. A surface's explicit profile takes precedence for
footsteps and impacts.

### Automatic integration helpers

Select the GameObject containing your player controller, Door, or Rigidbody.
Use **Tools > Horror Audio > Set Up Selected Objects**. This supports multiple
selected objects and Undo, and records prefab instance overrides. Running it again
preserves already assigned profiles and audio files.

- **Player:** adds/reuses PlayerFootstepAudio and SoundEmitter, and assigns the
  Footsteps profile when no direct clips are configured. PlayerFootstepAudio reads
  movement state from the controller and owns step timing. Existing AudioClip
  footsteps are imported when no new audio is assigned.
- **Door:** assigns opening, closing and latch profiles. Opening/closing sound
  starts with movement; the latch plays only when the closed rotation is reached.
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
| Impact | Short noise/tone transient for footsteps and hits |
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

On PlayerFootstepAudio, Sprint Footstep Gain and Crouch Footstep Gain change loudness;
the existing walk/sprint/crouch intervals still control cadence. Footstep Surface
Mask must include the floor layer. The player's own colliders are excluded from
the ground ray; floors can share the player's layer. A layer of gravel/noise can provide crunch without distorting the
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
fit. When capacity is full, the engine can replace lower-priority or substantially
less audible sounds. Native Unity priority also receives the emitter's resolved
priority. See Priorities and Capacity below.

The engine auto-creates one persistent root and reuses the scene's AudioListener.
Do not add extra listeners. Emitter loops stop when their owner disables; scene
objects clean up their own sources. Master Volume controls engine emitters only.

DialogueVoice keeps its existing syllable generation, per-note shaping, pitch
planning and timing, while SoundEmitter owns its playback. Its optional Output
Mixer field can route it into a shared mixer.
Dialogue and ItemInspector retain ownership of player.isInspecting. The sound
engine does not take keyboard or mouse input.

There are no pocket or train simulation scripts in this branch, so those have
not been rewritten. Use the public emitter/impact/train APIs when integrating
those systems into this branch. Dialogue emitters receive environment effects
when Use Environment is enabled; shared mixer routing remains optional.

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
authoring. Replace generated starter sounds with authored audio where realism matters.

Commit runtime scripts, editor tools, referenced profiles, this guide and their
.meta files together. Unity caches and local validation artifacts are excluded by
the project .gitignore.

### Player movement and audio separation

Adjust walk, sprint and crouch cadence on **PlayerFootstepAudio**. Assign your clips and effects on its **SoundEmitter**. The Player prefab and audio setup tools include both components. Disabling movement or inspecting an item stops new footsteps; removing PlayerFootstepAudio leaves movement working independently.

## Door audio, sequences and triggers

The Door component now owns motion and UnityEvents only. DoorAudio listens to
Opening, Closing and Closed. The latch plays only when the door actually reaches
its closed rotation; changing movement speed does not desynchronise it. Opened
and Motion Stopped are also available for UnityEvents. Disabling Door cancels its
motion and pending audio. The existing door prefab is already connected with the
DoorOpen, DoorClose and Latch starter profiles.

Select a door and use Tools > Horror Audio > Add Sound, Trigger = Door, or
Set Up Selected Objects. DoorAudio exposes Opening, Closing and Latched. For each
sound choose a direct Clip, a Profile, or a Sound Sequence. The selection order is
Sequence, Clip, Profile, then Use Emitter Sound. Direct clips use SoundEmitter's
EQ/effects; profiles own their effects. Volume and Pitch are per-event multipliers.
Use Emitter Sound intentionally shares the emitter's clip selection between events.
For different opening/closing files, assign each Clip on DoorAudio instead.

Create a sequence via Assets > Create > Horror Audio > Sound Sequence. Add Steps
with Clip or Profile, Volume, Pitch and Delay. Delay is in seconds from sequence
start, not from the previous step. Steps may be listed in any order. Equal delays
play together. Example: handle click at 0, metal release at 0.08, creak at 0.15.
Sequence event volume/pitch multiply each step's values. Profiles can contain
layers for simultaneous texture. No sample-accurate musical timing is promised;
sequence timing follows Unity frames. By default pausing the game pauses delays;
Use Unscaled Time is available on the asset.

SoundSequencePlayer exposes Play, PlaySequence, CancelPending and Stop to
UnityEvents and animation events. Starting an event replaces pending steps of
that player, while already playing one-shots finish naturally. Stop also stops
that player's emitter voices. Disabling the player stops both. Use a dedicated
emitter/player per independently controlled object; sharing an emitter also
shares Stop. Door closing completion replaces pending closing details with the
latch event. Keep details within the motion or drive them from a specific event.

For a zone: create an empty GameObject, use Add Sound with TriggerZone, then set
Entered and Exited on SoundTrigger. The tool adds a trigger box when no collider
exists. Resize it to your area. A moving actor needs a CharacterController or a
Rigidbody/collider setup for Unity trigger callbacks. Required Tag defaults to
Player and also recognises this project's FPS controller; empty accepts any tag.
Layers filter entering colliders. Multiple colliders belonging to the same
Rigidbody/player produce one entry and one final exit. Cooldown and Play Once
control repeats; ResetTrigger clears them. Stop On Exit cancels playback;
otherwise an unconfigured exit leaves the entry sound running. Trigger and Stop
are also callable from UnityEvents or animation events without a zone collider.

For movement-dependent sounds use Door events, animation foot contacts or
collision events. Use fixed delays for authored detail, rather than predicting
when a door will finish moving.

Supplying a file in the Add Sound door wizard replaces Opening/Closing sequence
selections with that file; Latched remains independent. Create your own sequence
assets through the Create menu when a sound needs several timed steps.

## Scaling audio on the train

SoundEngine manages global voice capacity and a shared acoustic-check allowance.
SoundEmitter exposes per-object limits, priority, optional prewarming and wall
check timing in its Advanced Inspector section.

### Priorities and capacity

SoundEmitter > Advanced > Priority uses Unity's order: lower numbers are more
important. -1 inherits the original source priority or profile priority. The
Player prefab uses 80, door prefab 96, and automatically created dialogue emitters
32. These are starting points, not reserved channels. Suggested background
priorities are 184-200, with ordinary object details around 128. A critical cue
can use a lower number, but assigning everything high priority removes this
distinction.

Admission checks the complete event against both emitter and global limits
before stopping any victims. Lowering a limit at runtime trims the least
important active layers on the next update. Equal-priority replacement requires a meaningful
estimated audibility advantage to prevent repeated swapping. The estimate uses
gain, layer level, simple distance falloff and cached occlusion; it is not a
measurement of rendered loudness, custom rolloff, or mixer processing. Unity
still handles its own real/virtual source selection using native priority.

Evicted looping layers defer and resume only when free capacity returns. Their
approximate playback phase advances using DSP elapsed time and pitch at eviction;
subsequent pitch changes during suspension are not integrated. This is separate
from Unity virtualization. A manual Stop, disable or reusing that emitter's final
free slot cancels its deferred layer. Dedicated background emitters are advised.
Replacement stops a victim immediately; this pass does not add crossfade tails.

Prewarm Voices prepares a per-emitter reusable pool and does not consume playback
capacity. Prefer small counts for frequently used footsteps/doors and zero for
idle objects. Pool slots remain with their emitter until it is destroyed. This
pass does not implement a shared global pool or shared room reverb buses.

Direct clip/variation/layer arrays are rebuilt only when their contents change.
Live settings update only profiles currently playing on that emitter. Nearby
voice controls update each frame, distant ones at 0.1s, native virtual ones at
0.25s; smoothing uses elapsed time between updates.

### Acoustic work budget

Idle, 2D, and sufficiently quiet/out-of-range voices skip wall checks. Native
virtual voices skip checks unless a cached obstruction needs refreshing, so
opening a previously blocking wall can restore the sound. Active near voices use Occlusion Interval (default 0.15s), with slower
checks at distance and staggered schedules. SoundEngine > Max Occlusion Checks
Per Frame caps emitter checks across the scene (default 32). Material thickness
may perform additional collider queries per check, so this is not a total raycast
count. Waiting emitters retain their last result. Zero disables new checks.

### Measure before selecting a production budget

Profile a representative gameplay scene after warm-up, with occlusion off/on
and bursts of object activity. Use a Development Build on target hardware with
Unity Profiler attached. The Audio module provides DSP CPU, memory and native
voice data. CPU Timeline markers HorrorAudio.Playback, HorrorAudio.ControlUpdate
and HorrorAudio.Occlusion isolate the engine's control work.

SoundEngine exposes ActiveVoiceCount, VirtualVoiceCount (a subset of active
voices), PooledVoiceCount, DeferredLoopVoiceCount, RejectedPlaybackCount,
StolenVoiceCount and OcclusionChecksThisFrame/Total for diagnostics. ResetStatistics
clears its cumulative counters. A replaced or deferred voice is not the same as
Unity virtualization. Functional checks do not establish a production hardware
capacity or a percentage speedup.

Unity's real/virtual voice settings remain in Project Settings > Audio and are
not automatically raised by this implementation. Room/door portal propagation,
shared mixer buses and final production mixing remain separate future work.

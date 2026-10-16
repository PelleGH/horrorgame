<<<<<<< Updated upstream
# Stationary Train Simulation — User Guide

The train interior stays stationary. Virtual acceleration, braking, turns, hills
and suspension motion generate forces on loose objects and feedback for the player.
Exterior scenery and audio can use the same simulation state to suggest travel.

For the optional train that physically moves, see [TrainIntegration.md](TrainIntegration.md).

For a windowed cabin and a moving procedural exterior, open
[TrainSceneryTest.unity](TrainSceneryTest.unity). See the
[rolling scenery guide](../Scenery/README.md) for settings and controls.

## 1. Test scene

Open [StationaryTrainTest.unity](StationaryTrainTest.unity) and press **Play**.
The environment is generated at runtime; only a bootstrap object is visible before
Play. The lab contains a stationary carriage, three bottles, a crate, a rolling
ball and a boarding platform with red/green command zones.

| Control | Action |
| --- | --- |
| WASD / mouse / Space | Walk / look / jump |
| Shift / Ctrl | Sprint / crouch |
| B | Toggle emergency brake |
| T | Toggle normal stopping/running; this does not release an emergency brake |
| R | Reset journey, player position and loose objects |
| F1 | Overview camera; walking/look input pauses |
| P | Toggle timetable/random journey and reset |
| Enter red platform zone | Emergency brake |
| Enter green platform zone | Depart and release emergency brake |

The default repeating timetable is:

| Stage | Duration | Target speed | Turn | Uphill angle | Roughness |
| --- | --- | --- | --- | --- | --- |
| Departure | 10 s | 8 m/s | 0 deg/s | 0 deg | 1 |
| Right bend | 8 s | 8 m/s | +8 deg/s | 0 deg | 1 |
| Left bend | 8 s | 8 m/s | -8 deg/s | 0 deg | 1 |
| Hill and rough track | 8 s | 6 m/s | 0 deg/s | +5 deg | 2 |
| Station stop | 12 s | 0 m/s | 0 deg/s | 0 deg | 1 |

During Play, select **Stationary train influence box** to adjust settings.
Play-mode edits are temporary. Configure your own scene/prefab outside Play for
permanent settings, or change the test bootstrap's generated configuration.

## 2. Install on your own train

1. Put the model beneath a train root whose local **+Z** points forward. An upright
   root at scale `(1,1,1)` makes setup straightforward; rotate/scale the model child.
2. Select the root and choose **GameObject > Train > Add Stationary Simulation to Selected Train**.
3. The command creates **Stationary train influence box**, containing a trigger
   BoxCollider and StationaryTrainSimulation. It estimates bounds from renderers.
4. Adjust the box's **Center** and **Size** to cover the playable interior, including
   tables, shelves and the player's body. Exclude nearby exterior areas.
5. Check **Train Frame** references the root. It defines force directions and the
   reference origin for rotational effects.
6. Add normal colliders to floors, walls and fixed furniture. Keep them stationary.
7. Disable TrainPathFollower or other scripts/animations that move the carriage.
   An existing carriage Rigidbody is made kinematic by this setup.

Suggested hierarchy:

```text
Scene
├── My Train                  ← stationary, +Z forward
│   ├── Model
│   ├── Floor / wall / furniture colliders
│   └── Stationary train influence box
│       ├── BoxCollider       ← Is Trigger
│       └── StationaryTrainSimulation
├── Player                    ← controller + StationaryTrainPassenger
├── Loose physics objects
└── Train command trigger boxes
```

The influence box selects affected objects; it is not a physical wall. Keep the
player independent of train parenting. Save the train and influence box as a
prefab to reuse the configuration. The test bootstrap is not needed in your game.

## 3. Loose objects

A prop needs a Collider and Rigidbody with **Is Kinematic off** and **Use Gravity on**.
It does not need an extra script to receive the simulation's forces.

The Rigidbody's **center of mass** must be inside the influence box, with a
non-trigger collider on an **Affected Body Layers** layer. Kinematic bodies,
trigger-only colliders and bodies without gravity are excluded. Ordinary gravity
comes from Unity and is not applied a second time.

For convenient configuration, select a prop and choose
**GameObject > Train > Make Selected Object Loose**. This adds LooseTrainObject.

| Setting | Purpose |
| --- | --- |
| Mass | Rigidbody mass; simulated inertia acceleration itself is mass-independent |
| Linear / Angular Damping | Resistance to movement / rotation |
| Override Center Of Mass | Use a configured local center instead of Unity's calculated center |
| Friction Material | Apply friction to the body's child colliders |
| Detach From Train On Start | Detach when a parent Rigidbody is found |
| Inherit Train Velocity On Start | Copy that parent's point velocity; normally zero on a stationary train |

The helper also configures interpolation, collision detection and solver
iterations. Use primitive/compound colliders or convex meshes for dynamic props.
Friction, shape and center of mass determine whether a bottle slides or tips.

Props may be arranged under a stationary root while authoring. With no parent
Rigidbody, the helper leaves them parented; a stationary parent does not carry
objects through space. Held objects made kinematic by ItemInspector stop receiving
forces until they become dynamic again. Do not add the virtual train's full
forward speed: all velocities in this system are relative to the carriage.

## 4. Player setup and customization

Select the object containing the existing Player.cs controller and CharacterController.
Choose **GameObject > Train > Add Stationary Passenger to Selected Player** before Play.

Player.cs already calls this component and combines its velocity/camera offsets
with normal movement. A different player controller needs equivalent integration;
adding the component alone does not move an unrelated controller.

| Stationary Train Passenger setting | Default | Effect |
| --- | --- | --- |
| Simulation | Empty | Auto-detect a containing box; an assigned simulation is preferred while its box contains the player |
| Force Multiplier | 1 | Player acceleration strength |
| Footing Resistance | 3.5 | Grounded horizontal resistance, in m/s² |
| Crouch Grip Multiplier | 1.6 | Additional resistance while crouched |
| Slip Damping | 0.4 | Reduces remaining grounded sliding |
| Maximum Slip Speed | 5 | Caps simulated extra velocity, in m/s |
| Lean Degrees Per Acceleration | 0.6 | Camera lean per unit of acceleration |
| Maximum Lean Degrees | 3 | Camera lean limit |
| Camera Response | 5 | Higher values follow feedback faster |
| Camera Bounce Multiplier | 0.35 | Vertical camera feedback strength |

Weak forces are resisted by footing; stronger ones can slide the player. Airborne
players receive acceleration without footing resistance. Grounded vertical
simulation velocity is restrained, while the existing controller supplies jumping
and gravity. Leaving a box stops new forces, but existing extra momentum settles
through footing or persists while airborne.

## 5. Journey modes

### Random Journey

The train selects a driving speed, changes bends/grades, brakes, waits at a station,
and departs again.

| Setting | Default | Purpose |
| --- | --- | --- |
| Random Seed | 42 | Repeatable random sequence after Reset Journey |
| Random Speed Range | 5–9 | Target speeds in m/s |
| Drive Duration Range | 20–40 | Driving seconds before requesting a stop |
| Station Dwell Range | 5–10 | Waiting seconds after speed reaches zero |
| Bend Duration Range | 5–12 | Seconds before choosing a new bend/grade |
| Maximum Random Turn | 8 | Maximum absolute target turn rate, in deg/s |
| Maximum Random Grade | 4 | Maximum absolute uphill/downhill angle |

Use Random Speed Range to change random cruising speeds. Cruise Speed acts as a
reference for motion intensity; it does not replace the random speed targets.
External stopping/braking pauses journey timers until travel resumes.

### Timetable

Add ordered stages with label, duration, target speed, turn rate, uphill angle and
roughness. Positive turns mean right, negative mean left. Positive grade means
uphill. Enable **Repeat Timetable** to repeat the stages.

Duration includes acceleration/braking. At 8 m/s with Braking = 2 m/s², stopping
requires approximately 4 seconds. A 12-second stop stage leaves approximately
8 seconds at rest. Random Journey's dwell timer instead starts after stopping.

### Manual

Use Cruise Speed, Manual Turn Degrees Per Second and Manual Uphill Degrees.
Set them through the Inspector, gameplay scripts or command triggers.

### Virtual Route

1. Choose **GameObject > Train > Create Route Only**.
2. Move waypoint objects and reorder the route's waypoint array.
3. Assign it to **Virtual Route** on the simulation and select this mode.
4. Set Cruise Speed and optional Route Start Distance.

The path supplies bends and grades while the model stays stationary. Open routes
stop at the endpoint; loops wrap. Open routes need at least two assigned points,
loops at least three, with nonzero length. Enable Scene Gizmos to preview the curve.
The smooth curve can bow beyond control points; use broad, reasonably even bends.
Use **Rebuild Route** after runtime edits; large changes can alter motion abruptly.

## 6. Movement settings

The ride Inspector uses collapsible groups and only shows the settings for your
selected journey mode. **Gentle / Normal / Rough** provide quick movement presets.
They change speed, braking, turns, hills and vibration, including existing timetable
targets; stage durations and stop stages are preserved. Undo restores previous values.

To keep and reuse your settings:

1. Choose **Assets > Create > Train > Ride Profile**.
2. Select the asset and edit its grouped settings or apply a preset.
3. Assign it to **Ride Profile** on your simulation. Enable **Apply Profile On Start**.
4. For the demo, assign it to the bootstrap object's **Ride Profile** before Play.
5. To keep a ride tuned during Play, click **Save Current Ride Into Assigned Profile**
   on the simulation. This writes the asset, so its settings persist after Play.

**Apply Assigned Ride Profile** reloads the asset's settings; during Play it also
restarts the journey. Profile assets store ride parameters and an independent copy
of timetable stages. Train/route scene references, box geometry, object force
filters/multipliers, player settings, running/brake flags and events remain on the
scene components. Profile edits are applied at startup or using the Apply button,
not automatically every frame. Without a profile, component settings are used.

| Simulation setting | Default | Effect |
| --- | --- | --- |
| Acceleration | 1.5 | Increasing speed, in m/s² |
| Braking | 2 | Normal speed reduction, in m/s² |
| Emergency Braking | 6 | Stronger speed reduction during emergency braking |
| Turn Response | 2 | Higher values follow target turns faster |
| Grade Response | 1 | Higher values follow target grades faster |
| Bounce Amplitude / Frequency | 0.02 / 0.7 | Suspension motion, in metres / Hz |
| Vibration Amplitude / Frequency | 0.0006 / 7 | Faster rail vibration, in metres / Hz |
| Roughness Multiplier | 1 | Vertical intensity, combined with stage roughness |
| Bank Degrees Per Turn Rate | 0.15 | Simulated roll associated with turns |
| Include Rotational Forces | On | Euler, centrifugal and Coriolis corrections around Train Frame |
| Object Force Multiplier | 1 | Scales loose-object acceleration |
| Maximum Acceleration | 20 | Computed acceleration cap before object/player multipliers |
| Affect Rigidbodies | On | Enables prop forces; player feedback is controlled separately |
| Affected Body Layers | All | Filters Rigidbody colliders, not player auto-detection |

In Random, Timetable and Manual modes, target turn rate is reduced at speeds below
Cruise Speed. Bounce/vibration fade with speed and stop at rest. A simulated grade
can continue affecting gravity while stopped on a hill.

Increase one setting at a time. Stronger turns/speed or lower prop friction produce
more sideways motion. Lower Footing Resistance makes the player slide more easily.
Vibration acceleration increases with frequency squared, so small amplitude changes
at high frequency can produce substantial forces.

## 7. Collision-box commands

Press **Create Command Trigger Box** in the simulation Inspector, then position
and resize it. The new TrainSimulationTrigger is linked to the simulation.

| Command | Result |
| --- | --- |
| Start | Resume running and release emergency braking |
| Stop | Request a normal stop |
| EmergencyStop | Request emergency braking |
| SetSpeed | Set cruise speed using Value |
| SetTurn | Set manual turn rate using Value |
| SetGrade | Set manual uphill angle using Value |

**Only Character Controllers** is on by default. **Actor Layers** filters entrants.
**Once Only** prevents repeated use until ResetTrigger(). **Switch To Manual**
makes speed/turn/grade commands take control from a journey mode; otherwise that
mode can continue supplying its own targets.

These boxes react to the player's physical entry in the stationary scene. They
do not detect virtual station arrival; use timetable stages for timed stops.
Activate() can also be called from UnityEvents or interaction scripts.

## 8. Gameplay integration

```csharp
using UnityEngine;

public class TrainControls : MonoBehaviour
{
    public StationaryTrainSimulation train;

    public void Depart() => train.StartTrain();
    public void StopNormally() => train.StopTrain();
    public void PullEmergencyBrake() => train.EmergencyStop();

    public void SetManualJourney()
    {
        train.mode = StationaryTrainSimulation.MotionMode.Manual;
        train.SetCruiseSpeed(8f);
        train.SetManualTurn(6f);
        train.SetManualGrade(0f);
        train.StartTrain();
    }
}
```

Assign the simulation reference in Inspector. Public methods can also be wired to
UnityEvents. **On Departed** fires when speed rises above approximately 0.01 m/s;
**On Stopped** fires when it falls to that threshold. Use them for doors, sound,
lighting or station gameplay.

ResetJourney() resets motion, timers and the random sequence. It does not change
Running/Emergency Brake flags or reposition props/player. The test scene's R key
performs that larger reset separately. FixedUpdate steps the simulation automatically;
do not also call SimulateStep()/ApplyToObjects() every frame during normal gameplay.

The model never moves. Speed, TravelDistance, ElapsedTime, TurnRate, Grade,
VerticalOffset, CurrentStage and CenterAcceleration are available for scenery/audio.
Exterior scrolling is a separate system: use Speed for scroll rate and stop at zero.
Distant scenery should move more slowly than nearby scenery. TravelDistance wraps
on a looping Virtual Route and is therefore not always a total-distance odometer.

## 9. Troubleshooting

| Problem | Check |
| --- | --- |
| Props do not react | Affect Rigidbodies on; dynamic body with gravity, non-trigger collider on allowed layer, center of mass inside box |
| Player does not react | StationaryTrainPassenger added before Play to the existing player; controller center inside box; footing may resist weak forces |
| Movement is too violent | Reduce turn rate, speed, vibration frequency, roughness or multipliers; adjust acceleration cap |
| Command does not activate | Simulation reference, Is Trigger, Actor Layers, CharacterController filter, Once Only state |
| Speed/turn setting is ignored | Use Manual mode or trigger's Switch To Manual |
| Edits disappear after Play | Configure the scene/prefab outside Play; demo objects are runtime-generated |
| Train physically moves | Disable movement scripts/animations; ensure carriage Rigidbody is kinematic |
| Boxes overlap | First enabled containing simulation owns the body; forces are not added together |

## 10. Limits and files

This assumes an upright CharacterController. Hills, banking and suspension are
simulated forces/camera feedback; floors do not actually tilt or deform. Grounded
vertical motion is restrained. This is a gameplay approximation, not a full
arbitrary-gravity or articulated-carriage simulation. Exterior scenery is not generated.

Use one box for a shared interior, or synchronize timing/settings across carriage
simulations. Overlapping boxes with different journeys are ambiguous and choose
one owner. Save reusable configurations as prefabs and commit scripts, scenes,
documentation and their `.meta` files to Git.

| File | Responsibility |
| --- | --- |
| [StationaryTrainSimulation.cs](../Scripts/StationaryTrainSimulation.cs) | Journey state, influence box, prop forces |
| [StationaryTrainPassenger.cs](../Scripts/StationaryTrainPassenger.cs) | Footing, extra velocity, camera feedback |
| [Player.cs](../Scripts/Player.cs) | Player movement, sprint stamina and passenger integration |
| [TrainSimulationTrigger.cs](../Scripts/TrainSimulationTrigger.cs) | Player-entry and event commands |
| [LooseTrainObject.cs](../Scripts/LooseTrainObject.cs) | Optional prop physics settings |
| [TrainRoute.cs](../Scripts/TrainRoute.cs) | Smooth waypoint geometry |
| [StationaryTrainTestEnvironment.cs](../Scripts/StationaryTrainTestEnvironment.cs) | Demo construction and controls |
| [TrainSetupEditor.cs](../Editor/TrainSetupEditor.cs) | Setup menus and custom Inspectors |

Use the reusable components in your game. StationaryTrainTestEnvironment is only
for the demonstration scene.
=======
# Stationary train simulation

## Apply to your model

1. Select your train root and choose **GameObject > Train > Add Stationary
   Simulation to Selected Train**. A child influence BoxCollider is fitted around
   the model. Adjust its Center and Size to cover only the playable interior.
2. Keep the model stationary. Disable Train Path Follower and other
   scripts/animations that move the carriage. The simulation never moves
   the train or its colliders. Its Train Frame reference defines forward (+Z).
   An existing carriage Rigidbody is made kinematic so it cannot fall under gravity.
3. Give floors, walls, tables, and fixed furniture normal colliders. Give loose
   props colliders and dynamic Rigidbodies with gravity. Any eligible Rigidbody
   whose center of mass lies inside the box receives simulated forces; it does
   not need a special script. Affected Body Layers filters which objects qualify.
4. Select your existing player (the object with CharacterController and the
   current Player script), then choose **GameObject > Train > Add Stationary
   Passenger to Selected Player**. It detects the simulation box automatically.
5. Choose a journey mode and press Play. Save your model plus the influence box
   as a prefab to reuse the configuration.

Do not use moving-platform inheritance to add the virtual train's forward speed
to objects or the player: all velocities here are relative to the stationary
carriage. The existing Moving Surface Passenger may remain for other moving
platforms; the stationary floor contributes no carrier velocity.

## Journey modes

| Mode | Setup |
| --- | --- |
| Random Journey | Set seed, speed range, driving duration, station dwell, bend duration, maximum turn rate, and maximum grade. The train brakes completely before the dwell countdown begins, then departs again. |
| Timetable | Add named stages with duration, target speed, turn rate, uphill angle, and roughness. A speed of zero requests a stop. Enable Repeat Timetable to repeat. |
| Manual | Set Cruise Speed, Manual Turn Degrees Per Second, and Manual Uphill Degrees; control start/stop through gameplay. |
| Virtual Route | Assign the editable Train Route from the earlier system. Its curve supplies bends and grades, but the train model stays still. Open routes stop at their endpoint. |

Durations are simulated seconds, not real-world clock times. Timetable stage
duration includes acceleration/braking, so allow enough time for speed to reach
zero if the stage represents a station. External Stop/EmergencyStop pauses
random/timetable progression until departure resumes. Reset Journey resets the
simulation clock/motion but does not reset props or the player.

## Tuning movement

- **Acceleration / Braking / Emergency Braking:** how rapidly virtual speed changes.
- **Maximum Random Turn:** bigger values mean tighter turns and stronger sideways forces.
- **Turn Response / Grade Response:** smoothness of changes to bends and hills.
- **Bounce Amplitude / Frequency:** vertical suspension motion (metres / Hz).
- **Vibration Amplitude / Frequency:** small, faster rail vibrations. Tiny amplitudes
  are intentional: acceleration grows with frequency squared.
- **Roughness Multiplier:** increases vertical effects; individual timetable stages
  also have a roughness value. Bounce/vibration fade with speed and stop at rest.
- **Bank Degrees Per Turn Rate:** simulated carriage roll through bends.
- **Include Rotational Forces:** Euler, centrifugal and Coriolis corrections relative
  to the train root. Center acceleration includes the bend's sideways acceleration.
- **Object Force Multiplier / Maximum Acceleration:** overall intensity and a force
  cap to keep extreme settings manageable.

Unity supplies ordinary gravity. The simulation adds acceleration/braking,
turning, virtual tilt gravity correction, and vertical vibration. It never adds
ordinary gravity a second time. Multiple overlapping influence boxes choose the
first enabled containing simulation, so forces are not doubled. Avoid overlapping
boxes with different journey configurations; for multiple carriages, use one box
covering the train or keep their settings/timing synchronized.

## Player footing and camera

Stationary Train Passenger exposes force strength, footing resistance, extra
crouch grip, slip damping, maximum slip speed, camera lean, and camera bounce.
Weak forces do not overcome grounded footing. Strong braking can slide the
player; airborne players respond without footing resistance. Camera feedback
comes from the same simulation and blends with the existing look/crouch/step
camera. The player's existing gravity remains in use.

All this assumes an upright CharacterController. Floor height/geometry stays
fixed: hills, banking and suspension are simulated forces and camera feedback,
not actual deformation/rotation of the carriage. Grounded vertical motion is
restrained by the floor; this is a controllable gameplay approximation, not a
full arbitrary-gravity vehicle simulation.

## Collision-box commands

Create a separate object with **Train Simulation Trigger**. Its BoxCollider becomes
a trigger. You can also use **Create Command Trigger Box** in the simulation's
Inspector to create and link it automatically. Assign your simulation and choose Start, Stop, EmergencyStop, SetSpeed,
SetTurn, or SetGrade. By default it responds only to CharacterControllers, with
an optional actor layer filter and Once Only. Speed/turn/grade commands switch to
Manual by default so random/timetable targets cannot override them.

The trigger activates when the player physically enters its box in the stationary
world. It does not represent the virtual train arriving at a world-space station;
use timetable stages for timed stops. `Activate()` can also be called from buttons
or UnityEvents, and `ResetTrigger()` allows a once-only trigger to run again.

Simulation methods: `StartTrain()`, `StopTrain()`, `EmergencyStop()`,
`SetCruiseSpeed(float)`, `SetManualTurn(float)`, `SetManualGrade(float)`.
On Departed and On Stopped events can drive doors, sound, lighting, or gameplay.

## Test and exterior scenery

Open **StationaryTrainTest.unity**, then press Play. It builds a stationary carriage
in a compact test lab with bottles, a crate, a ball, and a boarding platform.
The default repeating timetable demonstrates departure, right and left bends,
a hill with rough track, and a station stop. There is no moving-train component
or circular track in this environment.
Controls: WASD/mouse/Space, B brake, T stop/start, R reset, F1 overview,
P toggle timetable/random journey (also resets player and props).
Walk through the red platform pad to activate emergency braking, or the green
pad to resume travel. These pads demonstrate player collision-box commands.
Select **Stationary train influence box** during Play to change journey settings.

Exterior scenery scrolling is not generated by this system. `Speed`,
`TravelDistance`, `TurnRate`, `Grade`, and `VerticalOffset` are available for a
window-scenery/audio system. Keep scenery motion separate from interior physics.
>>>>>>> Stashed changes

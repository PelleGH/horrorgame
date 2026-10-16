<<<<<<< Updated upstream
# Train setup

For the recommended stationary interior, see [StationaryTrainSimulation.md](StationaryTrainSimulation.md).

## Optional train that physically moves

1. **GameObject > Train > Create Train and Route** creates a root with a placeholder floor and editable route.
2. Put your model under My Train and replace the placeholder with floor/wall colliders.
3. Keep root scale (1,1,1), forward +Z. Rotate/scale the model child if necessary.
4. Move Train Route waypoint objects in Scene view; array order defines travel order.
5. Choose Place Train at Route Start, then set speed, acceleration and braking.
6. Add Moving Surface Passenger to the existing player. Set Surface Layers to rideable floors; keep the player outside the train hierarchy.

Train Path Follower uses a kinematic Rigidbody and smooth distance-sampled
Catmull-Rom waypoints. Loops join their endpoints; open routes brake at the end.
Start Distance, Route Offset, Follow Slopes and Samples Per Segment are configurable.
Route Inspector buttons add waypoints or use direct children in hierarchy order.
Keep route points outside the moving train. Broad, evenly spaced bends work best;
the curve can extend beyond control points. Routes are cached at startup; Rebuild
Route refreshes edits, but changes during travel may cause abrupt repositioning.

Fixed train colliders should not have separate Rigidbodies or be marked Static.
Use primitive/compound colliders or convex meshes on moving bodies. Loose props
need dynamic Rigidbodies. **GameObject > Train > Make Selected Object Loose** adds
configurable mass, damping, center of mass, friction and collision detection.
Props authored under a train can detach at startup and inherit carrier velocity.

Moving Surface Passenger carries the player through translation and optional yaw,
preserving carrier momentum when jumping off. Grounded footing is firm; inherited
momentum and ground probe distance are configurable. This assumes an upright,
unit-scale player.

Control a physical train with StartTrain(), StopTrain(), EmergencyStop(),
ReleaseBrake(), SetCruiseSpeed(float), and ResetToRouteStart(). Reset only teleports
the train; reset occupants/props separately. Independently articulated carriages
and intermediate-station stopping are not implemented.

Save the train as a prefab; assign its scene route reference on each instance.
Do not run physical train motion and stationary train simulation together.
StationaryTrainTestEnvironment is only the test scene bootstrap.
=======
# Using your own train and route

For a stationary interior with simulated forces, random/timed journeys, and
command boxes, see [StationaryTrainSimulation.md](StationaryTrainSimulation.md).
The setup below is for a train that physically moves through the world.

## Quick start

1. Choose **GameObject > Train > Create Train and Route**. This creates an editable
   eight-point loop and a train root with a placeholder floor.
2. Parent your designed carriage under `My Train`. Replace the placeholder floor
   with your own floor/wall colliders. Keep root scale at `(1,1,1)` and root forward
   along local **+Z**. Rotate or scale a model child if your imported model differs.
3. Move the `Train Route` waypoint objects in Scene view. Their array order in the
   route Inspector defines travel order. Enable Gizmos to see the smooth curve.
4. Select `My Train`, use **Place Train at Route Start**, and adjust speed,
   acceleration, normal deceleration, and emergency deceleration. Press Play.
5. Add **Moving Surface Passenger** to your existing player, alongside its
   CharacterController and current player script. Set Surface Layers to your train
   floor layer. Keep the player as a separate root object.
6. Select a bottle or other prop and choose **GameObject > Train > Make Selected
   Object Loose**. Add suitable colliders and tune the new Loose Train Object
   settings. You may author it under the train; it detaches automatically in Play.

The runtime-generated `StationaryTrainTestEnvironment` is only for the stationary
test scene. Your actual game uses the reusable components above, without that
bootstrap. The old circular test environment has been removed.

## Using an existing train instead

Add **Train Path Follower** to its root (a Rigidbody is added automatically),
then assign a **Train Route**. The follower configures the root as a kinematic,
interpolated body. Fixed floors, walls, tables, and furniture should be child
colliders without separate Rigidbodies. A moving Rigidbody's MeshColliders must
be convex; simple compound BoxColliders usually make better carriage interiors.
Do not mark moving geometry Static.

Route points represent the root pivot, not the rails or the player's feet. Match
their height to your root, or use Route Offset. Keep the route and points outside
the train hierarchy so they do not move with the train.

## Route customization

- **Loop** closes the route. Open routes brake automatically at their last point.
- **Start Distance** picks the starting position in metres along the route.
- **Follow Slopes** tilts the carriage along hills. Disable it to keep it upright.
- **Samples Per Segment** controls distance/curve approximation quality.
- **Add Waypoint** and **Use Child Objects as Waypoints** are available in the route
  Inspector. Drag Scene objects into the array or reorder entries for other routes.
- The smooth Catmull–Rom curve can bow outside its control points. Inspect the
  preview, space points reasonably evenly, and use broad bends rather than corners.
- Routes are cached at startup. If you intentionally edit points during Play,
  use **Rebuild Route**. A changed route can reposition the train abruptly.

Speed is in metres/second; acceleration is in metres/second squared. Tighter bends
at higher speeds create stronger forces. This component controls one rigid carriage
or a rigidly attached train body. Independently bending multiple carriages needs
separate followers with spacing along the route; that is not implemented here.

## Props and the player

Loose Train Object configures gravity, mass, damping, optional center of mass,
friction material, interpolation, and collision detection. Use primitive/compound
colliders or convex meshes, not concave bottle meshes. The friction of both the
prop and the surface determines how easily it slides. No artificial train force
is required: collisions with the physically moving carriage provide it.

Moving Surface Passenger carries the player through train translation and optional
yaw rotation. It preserves carrier momentum on jumps or stepping off. Set
Inherited Momentum Multiplier to `1` for the default behavior. Ground Probe Distance
can be adjusted for your controller size. Current support assumes an upright,
unit-scale player on walkable surfaces; steep slopes and roll are not a full
arbitrary-gravity character system. Grounded footing is firm; sliding/stumbling
from acceleration remains a separate extension.

Do not parent the player under the moving train. Keep loose prop detachment enabled
for world-space physics. When spawning a prop on an already-moving train, spawning
it under the train lets the component inherit the carrier's point velocity.

## Controlling it from gameplay

```csharp
public TrainPathFollower train;

void Depart() => train.StartTrain();
void Arrive() => train.StopTrain();
void PullEmergencyBrake() => train.EmergencyStop();
void ChangeSpeed(float metresPerSecond) => train.SetCruiseSpeed(metresPerSecond);
```

The public methods can also be wired to UnityEvents. StopTrain slows down wherever
you call it; automatic stopping at intermediate stations is not implemented.
ResetToRouteStart teleports the train only; reset/reposition occupants and props
separately if using it during gameplay.

Save `My Train` as a prefab to reuse your design. The scene's route reference must
be assigned to each prefab instance, because scene objects are not stored inside
prefab assets. Save loose props with their settings as separate prefabs as needed.
>>>>>>> Stashed changes

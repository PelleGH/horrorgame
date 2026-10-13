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

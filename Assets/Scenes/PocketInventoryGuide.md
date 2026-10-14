# Pocket Inventory â€” Look-Down Opening

The pocket is a small physical cavity at the player's waist, visible through the
ordinary first-person camera. Look down and press E to open it. There is no
fade to black, separate inventory camera or player teleportation.

## Use it

1. Press Play in your normal scene. Player.cs adds PocketInventoryController when
   Enable Pocket Inventory is enabled.
2. Look down toward your waist and press **E**. A small dark opening reveals the coin, key and ball,
   softly illuminated by a warm center light.
3. Move your view to aim at an item and **left-click** to take it.
   **Scroll forward** to lean smoothly into the pocket. While zoomed in, walking,
   crouching and ordinary mouse look are locked. The cursor becomes visible and
   free inside the game window. Move the mouse to pan around the contents and
   point at an item to select it. **Scroll backward** returns to your original view
   and restores the previous cursor state. Wheel input supports both 1-unit and
   120-unit notches.
4. Press **G** to put the held item back.
   For the **ball**, zoom back out, aim into the world and **right-click** to throw.
   It leaves the inventory and uses gravity and bouncing collisions. Throwing is
   blocked while leaning into the pocket, during dialogue or item inspection.
5. Press **E** again or **Escape** to close and smoothly return from zoom. When not
   zoomed in, looking up also closes the opening. You must press E to reopen it.

The standalone **PocketInventoryTest.unity** uses the same controller. Its view
starts looking down. No character or trousers model is needed.

## Customization

Add PocketInventoryController to your player before Play if you want to configure
it in advance. Player.cs reuses the existing component. Player and World Camera
references are found automatically if left empty.

| Setting | Effect |
| --- | --- |
| Look Down Angle | Angle at which item interaction becomes available; default 50 degrees |
| Waist Offset | Pocket position relative to player feet and body facing; default (0, 1.28, 0). Height is used when Follow Crouch is off |
| Pocket Side Angle | Position and orientation around the body; default 135 degrees (back-right), 0 front, 90 right, -90 left |
| Pocket Side Distance | Horizontal offset toward Pocket Side Angle; default 0.18 m |
| Follow Crouch | Keeps the opening at Camera Pocket Distance below the camera while standing or crouching |
| Camera Pocket Distance | Distance below the eyes; default 0.32 m. Reduce to bring the opening closer |
| Pocket Scale | Physical cavity size; default 0.14, approximately 0.34 m across |
| Reveal Speed | How quickly it grows/shrinks into view as you look down/up |
| Center Light Intensity | Warm interior light strength, adjusted live |
| Lining Brightness | Darkness of the curved lining, adjusted live |
| Item Reach | Selection distance from the ordinary camera |
| Pocket Layer | Dedicated cavity layer; default 30 |
| Show Help | Show item hover/click label and held-item return hint |
| Create Default Interior | Build the small cavity and three sample items |
| Hand Anchor | Optional held-object display point |
| Scroll Sensitivity | Zoom amount per wheel notch; default 0.18 |
| Camera Smoothness | Speed of the lean and pan transitions |
| Pan Sensitivity / Pan Limit | Mouse sensitivity and maximum movement inside the pocket |
| Zoom View Distance | Closest camera distance in pocket-local units; default 1.1 |
| Throw Speed / Throw Lift | Forward velocity and upward boost for the ball; defaults 10 and 1.5 m/s |

The pocket sits diagonally to the right at 135 degrees from the body's forward
direction. Look down and turn right toward it; scroll still leans into that pocket.
The body-facing direction stays steady during downward free look so you can aim
at side items instead of rotating them with your gaze. Looking up lets the pocket's
facing catch up with the player again. Its position follows the player while walking.

Disable Enable Pocket Inventory on Player.cs before Play to opt out of automatic
setup. Disable/remove a manually added controller too if present.

## Rendering and interaction

PocketInventoryRoom creates actual miniature geometry at the waist, rather than
placing the player in a remote room. The hollow lining faces inward. A small
emissive glow and local point light illuminate the contents. The opening has a
folded fabric hem and stitches, with a tapered bag underneath. The light range is
scaled to the cavity; URP intensity is adjusted for physical scale. The ordinary
camera includes the pocket's layer and continues rendering the surrounding world.
The fabric rim has an uneven fold, contrasting hand stitches, side seams and a
small leather label. The camera's near clip is reduced to keep nearby fabric visible.
The project's normal rendering and ASCII effects apply to the pocket as well.

The inventory never changes player.isInspecting or enables/disables the world
camera. IsCameraLocked pauses player controls and camera-height updates during
the lean and smooth return. The pocket itself stays fixed during inspection, so
it does not follow the approaching camera. Disabling the inventory restores the
camera immediately. Dialogue and ItemInspector remain responsible for their own exclusive
interactions. PlayerInteract reports when those systems are busy, which temporarily
closes the pocket. While looking down, E belongs to the pocket; otherwise it handles
normal world interactions.

Selection uses a ray from the ordinary camera: the screen cursor while zoomed in,
or the center of the view while zoomed out. A targeted item highlights, and a
left-click moves it to Hand Anchor. Selecting another returns the previously held
item to the cavity. G restores the original slot. Use IsPointerOverPocket to
coordinate pointer input with other systems, such as weapons.

## API

```csharp
var inventory = player.GetComponent<PocketInventoryController>();

bool visible = inventory.IsPocketVisible;
bool inspectingPocket = inventory.IsCameraLocked;
bool targetingItem = inventory.IsPointerOverPocket;
PocketInventoryItem held = inventory.HeldItem;

inventory.SelectItem(item); // requires the pocket to be visible
inventory.ReturnHeldItem();
inventory.ThrowHeldBall(); // returns false when no throwable is held or camera is locked
```

The previous EnterPocket/ExitPocket and separate-camera APIs have been removed.

## Custom objects

Sample models and storage slots are constructed in PocketInventoryRoom. Replace
them with your own models or an authored cavity. Item roots need PocketInventoryItem
with index 0 and trigger colliders for selection, so the pocket cannot push the
player or collide with scenery. Call InitializeStorage() after placement to
record parent, position, scale, rotation, layers and collider states.

The sample ball enables Can Throw on PocketInventoryItem. Throw Mass, Bounce and
Thrown Scale configure its physics and world size (default diameter 0.16 m).
Thrown balls remain in the world; picking them back up is not implemented yet.

This remains a visual prototype: world pickup, other gameplay item use, capacity,
save/load and full body/cloth animation are not implemented. The held-item display
is presentation, not a complete equipment system.

## Troubleshooting

| Problem | Check |
| --- | --- |
| Opening is not visible | Enable Pocket Inventory, look farther down and press E, check Waist Offset and camera culling mask |
| Items are too dark | Increase Center Light Intensity; verify additional point lights are enabled in the render pipeline |
| Opening is too large | Reduce Pocket Scale |
| Pocket blocks too much of the view | Adjust Waist Offset or reduce Pocket Scale |
| Cannot target side items | Keep looking down and turn the view slightly; body-facing remains steady during this free look |
| Pocket disappears during inspection/dialogue | Expected: exclusive world interactions hide it |
| Settings reset after Play | Configure the player component outside Play |
| Cursor leaves the game after switching windows | Click the Game view to resume first-person mouse capture; losing focus exits pocket zoom |

Commit scripts, scene, documentation and their .meta files together.


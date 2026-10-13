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
4. Press **G** to put the held item back.
5. Press **E** again or look up to close the opening. You must press E to reopen it. Walking, jumping and normal mouse look stay active.

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
The project's normal rendering and ASCII effects apply to the pocket as well.

The inventory never changes player.isInspecting or enables/disables the world
camera. Dialogue and ItemInspector remain responsible for their own exclusive
interactions. PlayerInteract reports when those systems are busy, which temporarily
closes the pocket. While looking down, E belongs to the pocket; otherwise it handles
normal world interactions.

Selection uses a ray from the ordinary camera. A targeted item highlights, and a
left-click moves it to Hand Anchor. Selecting another returns the previously held
item to the cavity. G restores the original slot. Use IsPointerOverPocket to
coordinate pointer input with other systems, such as weapons.

## API

```csharp
var inventory = player.GetComponent<PocketInventoryController>();

bool visible = inventory.IsPocketVisible;
bool targetingItem = inventory.IsPointerOverPocket;
PocketInventoryItem held = inventory.HeldItem;

inventory.SelectItem(item); // requires the pocket to be visible
inventory.ReturnHeldItem();
```

The previous EnterPocket/ExitPocket and separate-camera APIs have been removed.

## Custom objects

Sample models and storage slots are constructed in PocketInventoryRoom. Replace
them with your own models or an authored cavity. Item roots need PocketInventoryItem
with index 0 and trigger colliders for selection, so the pocket cannot push the
player or collide with scenery. Call InitializeStorage() after placement to
record parent, position, scale, rotation, layers and collider states.

This remains a visual prototype: world pickup/drop, gameplay item use, capacity,
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

Commit scripts, scene, documentation and their .meta files together.


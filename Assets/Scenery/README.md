# Rolling Train Scenery

Open **Assets/Scenes/TrainSceneryTest.unity** and press **Play**. The test starts on a cold boreal night in a stationary, walkable 1980s-inspired
coach with a higher ceiling, forward-facing seats, longer passenger bays and viewing windows. A streamed
landscape, railway, vegetation and scheduled features move around it. The view
and the train's inertia use the same distance-based bends and hills.

The scenery is decorative exterior geometry. Keep the train, its interior,
player and physical objects in the stationary train simulation.

## Controls

| Input | Action |
| --- | --- |
| WASD / mouse | Walk and look inside the carriage |
| Shift / Ctrl / Space | Sprint / crouch / jump using the existing player |
| Up / Down | Adjust target speed |
| [ / ] | Decrease / increase bend strength |
| T | Stop normally, or depart and release the emergency brake |
| B | Emergency brake |
| R | Start a landscape journey with a new seed |
| F1 | Toggle the exterior overview; player movement pauses |
| N | Preview rain, clear, snow, then return to procedural weather |
| H | Hide/show controls |
| Escape | Release/capture the mouse |

Speeds in the Inspector use metres per second. The HUD displays km/h.

## Settings and reproducible journeys

Select **Assets/Scenery/ArcticNightJourney.asset** outside Play Mode.
**ForestJourney.asset** remains available as the earlier temperate example. This asset holds
the route, biome choices, feature schedule, visible section dimensions and
budgets. The demo uses a runtime copy of this settings asset.

**Randomize Seed On Start** creates a fresh journey each session. Read the
streamer's **ActiveSeed** to reproduce a particular run: enter that seed in the
saved settings and disable randomization, or call `landscape.UseSeed(seed)`.
`GenerateNewJourney()` chooses a new seed. Both begin at distance zero and reset
the train's journey state. **Rebuild Landscape** retains the current seed and
distance while applying authoring changes. Distance and random feature rules
repeat from the seed; time and trigger positions also depend on when and where
they fired.

The settings copy is shallow: referenced biome assets, feature assets and
prefabs remain shared Unity assets. Editing those assets during Play changes the
assets themselves. Make lasting changes outside Play, or duplicate the biome
or feature first when experimenting with a variant.

## Add your own trees, bushes and other assets

1. Create a biome with **Create > Train > Scenery > Biome**, or duplicate
   **Assets/Scenery/Biomes/Forest.asset** or **Meadow.asset**.
2. In the biome's **Population** tab, add named **Layers**, for example Canopy,
   Young Trees, Grass, Ferns, Leaf Litter and Fallen Branches. Use the preset
   buttons or **Add forest floor set** to start with configured categories.
3. Expand a layer and drop one or more prefab assets onto its drop area. Each
   becomes an equally weighted entry; expand **Weighted prefab choices** to edit
   rarity. Dragging an already listed model does not add a duplicate. Alternatively,
   lock the biome Inspector, select prefabs in the Project window, select a
   destination layer and press **Add selected Project prefabs**.
   Built-in Pine, Bush, Rock, Grass, Fallen Leaves, Branch, Dead Tree, Fern and
   Broadleaf Tree shapes work without external assets. A supplied prefab takes
   precedence over Builtin. An empty entry with Builtin None produces no visual.
4. Add the biome to **ArcticNightJourney > Biomes** and give it a positive **Weight**.
5. Play the scene. After changing settings during Play, use the RollingLandscape
   component's **Rebuild Landscape** context menu to update the generated world.

The important controls have separate purposes:

| Control | Meaning |
| --- | --- |
| Biome Weight | Relative chance of choosing this biome for a route interval; zero excludes it |
| Biome Length | Global length of a biome interval in metres |
| Biome Blend Distance | Transition distance for terrain character and colours |
| Population Density | Journey-wide multiplier for all biome population layers |
| Biome Density Multiplier | Multiplies all layers in this biome |
| Layer Density Multiplier | Scales the number of attempts for one category |
| Attempts Per Chunk | Base placement attempts for this layer in each section, before density multipliers |
| Spawn Chance | Chance of accepting an attempt; lower values make the layer sparser |
| Prefab Weight | Relative rarity within this layer; zero disables that entry |
| Distance From Track | Minimum/maximum lateral distance from the railway centre, in metres |
| Side | Left, right, or both sides of the railway |
| Placement | Uniform scattering or grouped Clusters |
| Cluster Size / Radius | Objects per group and the group's spread in metres |
| Min Spacing | Minimum spacing within this layer, including section borders; zero permits free placement |
| Noise Threshold / Scale | Reject sites using seeded spatial noise to create patches and gaps |
| Distance Density Curve | Acceptance multiplier across the lateral range: 0 is near the track, 1 is the far edge |
| Slope Range / Height Range | Accept sites only within the selected slope and terrain height relative to the railway |
| Scale | Random uniform multiplier applied to the authored prefab scale |
| Normalize Height / Height | Optionally size different models by their visual height in metres |
| Rotation Offset / Scale Multiplier | Per-choice corrections for imported model orientation or units |
| Surface Offset | Raise or sink the object relative to the terrain |
| Align To Slope / Max Slope | Follow the terrain normal, or reject steep placement sites |
| Yaw Range / Tilt Degrees | Random rotation around the up axis and small lean variation |
| Choice Tint | Palette multiplier for a built-in choice; imported materials stay unchanged |

Weights are relative, not percentages. Two trees with weights 9 and 1 are
selected approximately 90% and 10% of accepted attempts. The Inspector displays
each entry's relative selection percentage. **Spawn Chance 0.5** accepts
approximately half the layer's attempts before the other placement filters.
Base attempts are multiplied by journey, biome and layer density. The configured
budgets still cap the work; increasing density does not guarantee a fixed count.
Terrain slope, spacing, spatial masks, track clearance, feature exclusions and
capacity limits can reduce the final object count.

Keep prefab roots upright with local **+Y up**. A ground-level pivot simplifies
placement. Built-in visuals use the biome palette; imported prefabs retain their
own materials, meshes, LODGroups and animators. Biome terrain multipliers adjust
the global hill height and frequency; they do not change the railway's own grade.

Each layer keeps an independent random identity. Adding a new layer does not
consume the random stream of existing layers, so you can add ground cover without
randomizing the canopy again. New layers created through the preset buttons get
their own ID. Existing assets with an empty ID use the layer name; keep those
names stable when preserving a particular seeded layout. Reordering layers can
still change which objects fit into a shared performance budget. If you duplicate
a layer in Unity's list, its expanded Inspector offers **Duplicated variation ID:
create unique layout**. Use that button when the duplicate should occupy different
positions instead of sharing the original layer's random stream.

## Populate the whole window view

Build the scene in depth instead of spreading a single tree uniformly over the
whole terrain. The winter presets use multiple canopy and ground-cover layers.
For your own biome, a useful starting recipe is:

| Layer | Placement | Suggested distance from track | Purpose |
| --- | --- | --- | --- |
| Tall canopy | Clusters, varied height | 10-100 m | Overlapping silhouettes and a continuous forest backdrop |
| Young trees and bushes | Smaller clusters | 7-35 m | Fill gaps at eye level near the windows |
| Grass and ferns | Dense patches, slope aligned | 6-25 m | Break up the ground immediately beside the train |
| Leaves and fallen branches | Low, irregular patches | 7-30 m | Ground detail and occasional recognizable shapes |
| Dead trees and rocks | Lower attempt count or lower choice weight | 10-65 m | Sparse variations in silhouette and colour |

These distances are examples. The journey's **Track Clearance** always takes
priority. Keep large tree trunks far enough from the carriage, and use the
distance-density curve to concentrate smaller detail near the windows. A branch
with weight 1 beside three common variants with weights 8, 8 and 8 has about a
4% selection chance within that layer; place rare hero objects in their own
low-density layer if they need a separate spacing or distance rule.

The **Terrain and palette** tab controls terrain relief, snow and built-in
colours. **Microterrain Height / Scale** break up the ground outside the railway
corridor. **Variation** adjusts colour variation among built-in objects.
**Foliage Wind Strength** on the journey settings adds motion to supported
built-in foliage; it does not drive an imported model's wind shader.

Use imported meshes, materials, textures and suitable LODGroups when moving
beyond the prototype. The built-in forms provide readable density, placement
and variety for testing; they are not photorealistic vegetation assets.

## Bends, hills and inertia

Under **Route**, change **Turn Strength**, **Maximum Heading** and **Turn
Wavelength** to tune bends. A shorter wavelength changes direction more often.
**Hill Height**, **Hill Wavelength** and **Grade Strength** control the railway's
rise and fall. Global landscape **Hill Height / Hill Frequency** separately
shape the terrain surrounding the track.

The route supplies railway geometry, scenery placement and train turn/grade
targets. The same bend is visible through both sides of the carriage. Actual
train speed controls how quickly that route passes and how strongly a bend
affects passengers. Stopping the train stops the landscape.

The streamer limits effective curvature for the chosen landscape width so the
inside of a bend does not fold back through itself. To make tighter bends,
reduce **Half Width** and the viewing distance as needed. Raising Turn Strength
past that safety limit has no further effect. This is a procedural continuous
route; it does not read an arbitrary authored spline or terrain world.

## Tunnels, lakes and landmarks

Create reusable features with **Create > Train > Scenery > Feature**. The
included **Tunnel.asset** and **Lake.asset** work without imported models.
Add feature rules under **ArcticNightJourney > Features**.

| Schedule | Placement |
| --- | --- |
| Distance | Once, starting at Start Distance metres |
| Time | Once after Start Time journey seconds; placed ahead of the train |
| Repeating Distance | Starting at Start Distance, then every Repeat Distance metres |
| Random | Deterministic irregular spacing within Random Spacing, starting from Start Distance |

**Chance** accepts or rejects each scheduled occurrence. If chance rejects an
occurrence, the gap between visible features can exceed Random Spacing. **End
Distance** prevents new starts beyond that distance; zero means unlimited. An
occurrence may finish beyond End Distance. **Allowed Biomes** optionally restricts
starts to selected biome assets; an empty list allows every biome.

**Length** runs along the railway. **Side** and **Offset From Track** place a lake
or landmark to the left or right. Both selects a seeded side per occurrence;
tunnels remain centred on the track. Feature **Width / Height** control its
cross-section. Rule **Scale** multiplies width, height and length. For example,
use a right-side lake offset 75 metres, width 90 metres and length 140 metres.
Leave enough room between its shore and the track.

Built-in tunnels enclose the railway with a clear interior; choose dimensions
that clear your train's sides and roof. Lake offsets are kept outside the safe
railway corridor, including when a requested offset is too small. Built-in lakes carve the terrain and
use a level water surface rather than placing water over a hill. Water Depth
controls the depression. Avoid overlapping conflicting feature rules; the
per-section feature budget gives earlier rules priority.

A Time rule schedules the feature at the train's current distance plus
**Feature Look Ahead** when the threshold is reached. It does not guarantee
arrival at that time. This also works while the train is stopped and avoids
materializing a feature around the cabin.

Supply a feature prefab to replace its built-in visual. The model should use
**+Z forward, +Y up**. Its measured bounds are centred across the feature,
centred along its scheduled length and grounded automatically, so its original
pivot can be offset. The bounds are normalized to the requested width, height
and length; this can stretch a model.
A custom feature is a rigid module and does not bend its own mesh. Use short
modules for curved tunnels and inspect clearance on the tightest bend.

Runtime triggers or gameplay code can request a feature:

```csharp
landscape.RequestFeature(lake, leadDistance: 180f, featureLength: 140f,
    side: ScenerySide.Right, offset: 75f);
```

Add **SceneryFeatureTrigger** to a carriage trigger zone or another object.
Assign the streamer and feature, then configure the lead distance, length and
side. A trigger collider can activate it for a Player-tagged character; a
UnityEvent can call its public **Trigger()** method directly. **Trigger Once**
prevents repeated requests until **ResetTrigger()** is called.

Features referenced by settings rules are prewarmed at startup. A rule with
**Chance 0** can prewarm a feature used only by triggers. An explicit runtime
request for an unregistered custom prefab prepares its pool once, which can
cause a small loading spike; register large models beforehand. Requests and
time events use a bounded queue. Schedule events ahead of the train rather than
requesting them every frame.

## Integrate your own train

1. Keep your designed train and interior at a fixed world position. Configure
   its existing **StationaryTrainSimulation**, influence box and passengers.
2. Add **RollingLandscape** to a separate upright root at unit scale. Put its
   origin at the railway centre under the train, with local **+Z forward**.
3. Assign a **LandscapeSettings** asset and the train simulation as **Speed
   Source**, or initialize both from code:

   ```csharp
   landscape.Initialize(journeySettings, trainSimulation);
   ```

4. Ensure a camera can see outside the train. Configure fog and clipping for the
   visible section buffer; the demo sets these up for its own cameras.
5. Remove the demo bootstrap when using your own train/player. It exists to
   build the sample carriage, lights, controls and HUD.

Initialization binds the streamer's route to the train simulation's
**MotionPath**. This supplies turning and grade in the existing motion modes;
their speed, start and stop behaviour stays in control. Disabling or destroying
the streamer restores its previous path if that binding is still current.
Without a Speed Source, **Fallback Speed** moves the scenery. Do not also move
the physical carriage through the generated world.

Exterior prefab copies have collisions, rigidbody physics, AudioSources and
custom gameplay scripts disabled before activation. They are visual scenery;
prefabs should not perform gameplay work in Awake. Keep interactive doors,
loose objects, sound emitters and player collision inside the train's stationary
simulation. Imported prefab materials and LODs still need to suit your render
pipeline and draw-distance budget.

## Streaming and budgets

The default buffer has nine 80-metre sections. Sections are recycled around the
train; absolute journey coordinates keep terrain, biome choices and feature
positions repeatable. Visible coordinates remain near the stationary cabin
even after long journeys. Retain sections behind the train for rearward views
and use fog to hide the buffer edges.

Built-in scenery is batched into reusable meshes. Custom prefab copies are
prewarmed and reused. **Max Copies Per Prefab Per Chunk** limits each choice's
pool, and **Max Features Per Chunk** limits feature geometry per section. More
biomes, layers and prefab choices increase prewarm memory even if a choice is
rare. **Max Custom Prefab Instances Per Chunk**, **Max Built In Vertices Per
Chunk** and **Max Placement Attempts Per Chunk** cap total section work.
New settings default to 512 custom instances, 200,000 built-in vegetation vertices
and 8,192 attempts. The richer Arctic preset uses 768 custom instances and
350,000 built-in vertices per section. Entries later in the lists can run out of budget. Start with
modest counts and use appropriate LODGroups on detailed models. The runtime
Inspector reports population, built-in vertex count and whether a section hit
a population limit.

Ordinary travel reuses the pooled objects and meshes. Initialization and an
explicit **Rebuild Landscape** can allocate and instantiate to reflect changes
to the authoring setup. Rebuild after changing asset lists, dimensions or pool
sizes; this is an authoring operation rather than an effect to call each frame.

Generated geometry uses **Train/Scenery Vertex Color**, a fogged stylized shader
with inexpensive ambient, moon and carriage-beam shading, subtle surface grain
and vertex wind on procedural foliage. It supports the built-in renderer
and URP without replacing imported prefab shaders. It does not simulate water
reflections, physically based exterior lighting or terrain collision.

The default shader is included through **Assets/Scenery/Resources** so new
settings assets also work in a player build. **Generated Shader** can override
it with a compatible vertex-colour shader; imported prefabs keep their shaders.

## Inspector shortcuts

The Landscape Settings inspector groups journey, population, weather, features,
terrain and performance limits. **Create and assign a biome** creates an asset
and adds its reference in one step. A biome has **Population** and **Terrain and
palette** tabs. Expand individual layers to edit distribution, size and weighted
models. Preset buttons add trees, bushes, rocks, grass, leaves, branches, ferns,
dead trees or broadleaf trees without changing existing layers. Each operation
supports Undo.

During Play, the RollingLandscape Inspector shows the runtime seed, current
biome, population and effective bend strength. **Apply population / rebuild
with the same seed** applies edits at the current journey distance; **Generate
a new journey** changes the seed and restarts. Rebuilding can instantiate or
resize pools, so use it for authoring, not continuously during gameplay.

## Carriage dimensions and layout

The demo's carriage settings expose coach width, length, ceiling height,
compartment count, seat pitch, aisle and seat dimensions. The larger starting
coach is approximately **4.8 m wide**, **23.5 m long** with a **3.35 m ceiling**.
Seats face local **+Z**, the front of the train. Repeated windows, upper shelves,
doorways, partitions and warm lamps make the passenger space readable while
leaving a continuous walkable aisle. Carriage geometry is built when Play starts;
restart Play after changing structural dimensions.

**Seat Prefab** replaces the generated chairs. Supply a ground-level pivot with
**+Z forward** and ensure the model fits the chosen seat width and depth; its
authored scale and colliders are preserved. Palette colours and optional
material overrides control the floor, walls, wood, seats, trim, shell and
curtains. Curtains, luggage racks, radiators and floorboard details can be
toggled separately. Disable **Batch Static Geometry** if an imported chair
needs to move independently.

The demo also sizes its influence volume, exterior precipitation envelope and
view around the coach. When replacing this generated carriage with your own
train, configure those dimensions for your model and keep the scenery root
aligned with the track. The generated interior remains a period-inspired
prototype rather than a licensed reconstruction of a specific 1980s coach.

## Cold night preset and weather

The test scene now references **ArcticNightJourney.asset**. **SnowboundTaiga**
and **WhiteoutForest** supply dense near-track spruces, a second forest layer,
young pines, shrubs and snow-covered rocks. Their palettes make the ground
snowy, and **Snow Cover / Snow Color** add caps to the built-in vegetation.
Imported models retain their own materials; use snowy model variants or their
own shader controls for imported assets.

Change **Weather** on the journey settings:

- **Initial Weather / Initial Weather Distance** guarantees the opening mood.
  The preset starts in heavy snow for the first 350 metres.
- **Section Length / Transition Distance** control seeded weather intervals and
  distance-based crossfades. Snow and rain emission blend during transitions.
- **Snow / Rain / Clear Weight** set relative chances after the opening.
- **Overrides** define authored distance intervals. The preset demonstrates rain
  around 650-1010 metres and calm weather around 1150-1450 metres, with fades.
- **Max Particles** caps all six exterior particle systems together. Rates,
  flake/streak size, fall speed, wind and snow flutter are adjustable separately.
- **Visibility Reduction** brings the fog closer in heavier weather.
- **Tunnel Precipitation** defaults to zero: entering a tunnel clears and
  suppresses the local precipitation volumes.
- **Carriage Interior Center / Size** defines the dry envelope in carriage-local
  coordinates. Adjust this when replacing the coach with a different model.

After changing particle capacity, size or flutter during Play, use the weather
component's **Rebuild Weather Volumes** context menu. Disabling the component
stops and clears its particle systems.

Precipitation streams backwards with actual train speed and responds to turns.
Snow/rain still fall when the train stops, but the distance-based weather pattern
does not advance. A bounded particle pass removes particles that enter the
carriage envelope or travel below the ground/too far away. These are decorative
local weather effects, without snow accumulation physics or collision audio.

On **Train Scenery Test**, change **Night Ambient**, **Moon Intensity**,
**Interior Lamp Intensity**, **Window Light Leak**, and **Window Light Reach**.
The brighter compartment lamps spill warm, diffuse light through the windows.
There are no headlight or floodlight fixtures on the demo coach. Window spill
uses short-range point sources, with a soft outward fade on generated scenery
and precipitation. Its intensity follows the cabin lamps, so turning those off
also removes the spill; the wider forest remains dark. The compartment lamps
use ordinary scene lighting. The demo's ambient/fog settings are restored when
it is destroyed. Replace the generated coach with your own model when ready;
put the weather component on the same stationary, track-aligned parent as the
landscape, then initialize it with your stationary carriage transform:

```csharp
weather.Initialize(settings, landscape, simulation, carriage);
weather.SetLighting(ambientColor, exteriorSpotlights);
landscape.SetSceneLighting(ambientColor, exteriorSpotlights);
```

Call the lighting methods when your lights change or move. Use
`SetManualWeather(SceneryWeatherKind.Rain)` for scripted previews and
`ResumeProceduralWeather()` to return to the seeded journey. **N** offers the
same previews in the sample scene. The coach is a procedural period-inspired
blockout, ready to replace with production meshes and materials.

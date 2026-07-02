# Flatpack Panic — Unity First-Person Prototype

Chaotic IKEA-like delivery simulator prototype.

## Current version
The game is now **first-person on foot** and **third-person chase camera while driving**.

You play as the short/fat mover **Borya Barrel**. The tall/skinny mover **Slender Sanya** is currently a visible NPC placeholder; next step is making him the navigator/split-screen player.

## Run
1. Open this folder in Unity Hub:
   `/Users/auryxn/.openclaw/workspace/flatpack-panic-unity`
2. Open scene:
   `Assets/Scenes/Prototype.unity`
3. Press Play.

## Controls
- `W / S` or `↑ / ↓` — gas and reverse when inside van; walk forward/back on foot
- `A / D` or `← / →` — steer when inside van; strafe on foot
- Mouse — look around
- `Shift` — sprint on foot
- `Space` — jump on foot / brake in van
- `E` — grab/drop/load/take cargo box; a prompt appears when cargo can be picked up
- `F` — enter/exit van; entering only works near the driver door
- `R` — reset cargo
- `H` — hide/show HUD and prompts
- `Esc` — unlock mouse
- left click — lock mouse again

## Implemented
- first-person walking camera and body
- enter/exit van flow
- third-person chase camera while driving so the road and van are visible
- proper van physics with Rigidbody + four WheelColliders
- visible wheel meshes synced to WheelColliders
- low CenterOfMass, suspension, anti-roll, braking, rear-wheel drive
- forgiving cargo pickup with SphereCast + nearest-cargo fallback
- pickup prompt and yellow highlight when cargo can be grabbed
- hideable HUD/prompts with `H`
- driver-door-only van entry prompt: `Press F to enter van`
- safer van exit points away from wheels
- steadier held-cargo physics
- clear green delivery beacon and delivery-complete result screen
- physical cargo with mass, drag, angular drag, damage from impact
- cargo jolts during harsh driving
- pickup zone, delivery zone, toy city, warehouse, apartment block
- HUD with score/damage/rank

## Next
- split-screen: Borya driver + Sanya navigator
- proper carry mechanics with two hands / two players
- wheel-collider vehicle model if needed
- stairs/elevator delivery interior
- random events: broken lift, angry grandma, wrong address, rain, parking ticket

## Gameplay loop prototype
1. Pick up boxes in the yellow warehouse zone.
2. Carry a box into the open rear cargo bay; when prompted, press `E` to load it. It disappears and is stored in the van inventory.
3. Drive to the green delivery zone.
4. Stand behind the van and press `E` to take a stored box. It appears in your hands.
5. Drop the box in the green zone to deliver it.
6. HUD tracks loaded cargo, delivered cargo, timer, damage, and final rank.
7. When all boxes are delivered, a result screen appears; press `R` to restart.

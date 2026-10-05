# CameraUnlock

[Source code on GitHub](https://github.com/L1GHTSHAPER/CameraUnlock) | [Report an issue](https://github.com/L1GHTSHAPER/CameraUnlock/issues) | [Thunderstore](https://thunderstore.io/c/on-together/p/LightShaper/CameraUnlock/)

A BepInEx mod for [On Together](https://store.steampowered.com/app/2688490/On_Together/) that frees the camera for screenshots.

The game's camera only zooms between about 2.3 and 4.3 m and tilts about 70°, and the tilt flattens out as you zoom out. This mod turns it into a real orbit around your character, adds field-of-view control, a free fly camera and a key that hides the interface.

## Features

- **Orbit camera**: the mouse wheel zooms from 0.4 m (close-up) to 40 m; holding the right mouse button tilts all the way to a straight **top-down view** or down to looking up from below. Takes over from the game's camera without a jump.
- **Field of view**: Shift + wheel narrows or widens the view (telephoto / wide angle), Shift + middle click resets it. A default can be set in the config.
- **Free camera** (**F6**): detach the camera and fly anywhere; your character stays where it is and ignores the keyboard meanwhile. Optional smooth, cinematic movement.
- **Hide the interface** (**F7**) for clean screenshots; take them with Steam's F12 as usual.
- Optional: turn off camera collision to look through walls and ceilings.
- Purely client-side: nothing is sent to other players.

## Controls

| Action | How |
|---|---|
| Zoom in / out | Mouse wheel |
| Tilt / rotate | Hold right mouse button and move the mouse |
| Field of view | Shift + mouse wheel; Shift + middle click resets |
| Free camera on / off | **F6** |
| Fly (free camera) | W A S D, E up, Q down, hold right mouse to look |
| Faster / slower | Hold Left Shift / Left Ctrl; the wheel changes the base speed |
| Hide / show the interface | **F7** |

Hotkeys are ignored while you are typing in the chat or any other text field.

## Configuration

`BepInEx/config/ontogether.cameraunlock.cfg` (created on first launch; editable from the mod manager's Config editor).

| Section | Key | Default | Description |
|---|---|---|---|
| Orbit | `Enabled` | `true` | Use the orbit camera. Off: the game's own camera. |
| Orbit | `MinDistance` | `0.4` | Closest distance, metres. |
| Orbit | `MaxDistance` | `40` | Farthest distance, metres. |
| Orbit | `MinPitch` | `-75` | Lowest angle, degrees (negative = from below). |
| Orbit | `MaxPitch` | `89` | Highest angle, degrees (89 = top-down). |
| Orbit | `ZoomStepPercent` | `12` | Distance change per wheel notch. |
| Orbit | `TiltSensitivity` | `1` | Vertical mouse multiplier. |
| Orbit | `CameraCollision` | `true` | Keep the camera in front of walls and the ground. |
| Lens | `FieldOfView` | `0` | Default field of view in degrees; 0 = the game's. |
| Lens | `ShiftWheelChangesFov` | `true` | Shift + wheel changes the field of view. |
| FreeCamera | `ToggleKey` | `F6` | Free camera on/off. |
| FreeCamera | `Speed` | `4` | Flying speed, m/s. |
| FreeCamera | `FastMultiplier` / `SlowMultiplier` | `4` / `0.25` | Speed while Fast / Slow is held. |
| FreeCamera | `LookSensitivity` | `2` | Mouse look sensitivity. |
| FreeCamera | `InvertY` | `false` | Invert vertical look. |
| FreeCamera | `Smoothing` | `0` | 0 = none, 0.9 = very smooth movement. |
| FreeCamera | `HideUiWhileFlying` | `false` | Hide the interface automatically in the free camera. |
| FreeCamera.Keys | `Forward` `Back` `Left` `Right` `Up` `Down` `Fast` `Slow` | `W S A D E Q LeftShift LeftControl` | Free camera keys. |
| Screenshots | `HideUiKey` | `F7` | Hide/show the interface. |
| Screenshots | `HideWorldSpaceUi` | `false` | Also hide canvases placed in the world. |
| Screenshots | `ShowHints` | `true` | Short on-screen hints (never shown while the interface is hidden). |

## Notes

- The swan boat uses its own camera, so the free camera is not available there.
- While the free camera is on, your character does not react to the keyboard; press F6 to get back. The chat (Enter) and the menu (Esc) still work.
- Fishing, basketball and drawing use their own cameras; the orbit only changes the normal walking camera.

## Installation

**Thunderstore Mod Manager / r2modman:** install from the mod list, or use *Settings -> Import local mod* with the package zip.

**Manual:** install [BepInExPack](https://thunderstore.io/c/on-together/p/BepInEx/BepInExPack/) and copy `CameraUnlock.dll` into `BepInEx/plugins/`.

---

## Русский

Мод снимает ограничения камеры — для красивых скриншотов.

- **Колесо мыши** — отдаление от 0.4 до 40 м; **правая кнопка мыши** — наклон вплоть до вида строго сверху или снизу.
- **Shift + колесо** — угол обзора (FOV), **Shift + средняя кнопка** — сброс.
- **F6** — свободная камера: W A S D — полёт, E/Q — вверх/вниз, правая кнопка — осмотреться, Shift/Ctrl — быстрее/медленнее, колесо — базовая скорость. Персонаж стоит на месте.
- **F7** — скрыть/показать интерфейс; скриншот — как обычно, F12 в Steam.
- Настройки — в `BepInEx/config/ontogether.cameraunlock.cfg` (или в Config editor менеджера модов); там же можно отключить столкновения камеры со стенами.

## Building from source

Requires Windows, .NET SDK 6.0 or newer, an installed copy of On Together, and BepInEx 5 (for example, a Thunderstore Mod Manager / r2modman profile).

From the repository directory, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -GameDir "C:\path\to\On-Together" -BepInExCore "C:\path\to\profile\BepInEx\core"
```

`GameDir` must contain `OnTogether.exe` and `OnTogether_Data\Managed`. `BepInExCore` must contain `BepInEx.dll` and `0Harmony.dll`. Game and BepInEx assemblies are referenced locally and are not distributed in this repository.

The build creates the plugin DLL in `src/bin/Release/` and the installable Thunderstore archive in `dist/`. Ready-to-install archives are also available in [GitHub Releases](https://github.com/L1GHTSHAPER/CameraUnlock/releases).

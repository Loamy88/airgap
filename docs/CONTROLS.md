# AIRGAP — Control Bindings (Phase 0 Part C)

Authoritative reference for `Assets/Settings/AirgapControls.inputactions`. The Infiltrator plays on gamepad or keyboard+mouse; the Warden is keyboard+mouse only (screen-switching and guard-clicking want precise pointing).

## Action map: `Infiltrator`

| Action | Type | Keyboard/Mouse | Gamepad |
|---|---|---|---|
| Move | Value (Vector2) | WASD | Left stick |
| Aim | Value (Vector2) | Mouse position | Right stick |
| Sprint | Button (hold) | Left Shift | Left stick press |
| Crouch | Button (toggle) | Left Ctrl / C | B / Circle |
| Interact | Button | E | A / Cross |
| UseGadget | Button | Left Mouse | Right trigger |
| CycleGadget | Button | Tab / Scroll | Y / Triangle |
| ToggleFlashlight | Button | F | D-pad up |
| ToggleBlueprint | Button | M | Select/Back |
| Drop | Button | G | D-pad down |

Stance model: crouch is a toggle, sprint is a hold that overrides crouch while held (Phase 1 controller resolves crouch/walk/sprint from these two).

## Action map: `Warden`

| Action | Type | Keyboard/Mouse |
|---|---|---|
| Screen1 (Camera Bank) | Button | 1 |
| Screen2 (Guard Comms) | Button | 2 |
| Screen3 (Facility Deployment) | Button | 3 |
| NextScreen / PrevScreen | Button | Tab / Shift+Tab |
| Point | Value (Vector2) | Mouse position |
| Select | Button | Left Mouse |
| ContextAction | Button | Right Mouse |
| Cancel | Button | Escape |

## Action map: `Session` (both roles)

| Action | Type | Binding |
|---|---|---|
| MenuToggle | Button | Escape / Start |

Notes:
- Active input handling is *Both* (Player Settings), so legacy `Input.GetAxis`/IMGUI stay available during greybox prototyping; shipping code polls the action maps.
- The Warden deliberately has no movement bindings — there is no Warden avatar.

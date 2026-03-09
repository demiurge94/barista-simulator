# First-Person Player Controller — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a functional first-person player controller (walk, sprint, jump, mouse look, E-key interact hook) to the Barista Simulator Unity project so it's ready for coffee shop asset integration.

**Architecture:** CharacterController on a Player root GameObject drives movement; a PlayerCamera child handles mouse look and raycasting for interaction. Three focused MonoBehaviour scripts keep concerns separated and easy to extend later.

**Tech Stack:** Unity 6 (6000.3.5f2), URP, C#, legacy `Input` API (no Input Actions asset needed)

---

## Pre-Task: Save Plan & Design Doc

**Files:**
- Create: `docs/plans/2026-03-08-first-person-controller.md` (copy of this plan)
- Create: `/Users/erick/demisidian/matador/comp565/first-person-controller-plan.md` (copy for class)

**Step 1: Create docs/plans directory and save plan**

```bash
mkdir -p /Users/erick/barista-simulator/docs/plans
cp /Users/erick/.claude/plans/sprightly-yawning-origami.md \
   /Users/erick/barista-simulator/docs/plans/2026-03-08-first-person-controller.md
cp /Users/erick/.claude/plans/sprightly-yawning-origami.md \
   /Users/erick/demisidian/matador/comp565/first-person-controller-plan.md
```

**Step 2: Commit the plan**

```bash
cd /Users/erick/barista-simulator
git add docs/
git commit -m "docs: add first-person controller implementation plan"
```

---

## Task 1: PlayerMovement.cs

**Files:**
- Create: `Assets/Scripts/PlayerMovement.cs`

**Step 1: Write the script**

```csharp
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] float moveSpeed = 4f;
    [SerializeField] float sprintSpeed = 8f;
    [SerializeField] float jumpHeight = 1.2f;
    [SerializeField] float gravity = -9.81f;

    CharacterController _controller;
    Vector3 _velocity;

    void Start()
    {
        _controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Reset vertical velocity when grounded
        if (_controller.isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : moveSpeed;
        Vector3 move = transform.right * x + transform.forward * z;
        _controller.Move(move * speed * Time.deltaTime);

        if (Input.GetButtonDown("Jump") && _controller.isGrounded)
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }
}
```

**Step 2: Verify it compiles**

In Unity Editor, check the Console panel — no errors should appear after Unity recompiles (bottom of the Editor shows a spinning icon while compiling).

**Step 3: Commit**

```bash
git add Assets/Scripts/PlayerMovement.cs Assets/Scripts/PlayerMovement.cs.meta
git commit -m "feat: add PlayerMovement with walk, sprint, jump, and gravity"
```

---

## Task 2: CameraLook.cs

**Files:**
- Create: `Assets/Scripts/CameraLook.cs`

**Step 1: Write the script**

```csharp
using UnityEngine;

public class CameraLook : MonoBehaviour
{
    [SerializeField] float mouseSensitivity = 100f;
    [SerializeField] Transform playerBody;

    float _xRotation;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -80f, 80f);

        transform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseX);
    }
}
```

**Step 2: Verify it compiles**

Check Console in Unity Editor — no errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/CameraLook.cs Assets/Scripts/CameraLook.cs.meta
git commit -m "feat: add CameraLook with mouse look and cursor lock"
```

---

## Task 3: PlayerInteract.cs

**Files:**
- Create: `Assets/Scripts/PlayerInteract.cs`

**Step 1: Write the script**

```csharp
using UnityEngine;

public interface IInteractable
{
    void Interact();
}

public class PlayerInteract : MonoBehaviour
{
    [SerializeField] float interactRange = 2f;

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, interactRange))
        {
            if (hit.collider.TryGetComponent(out IInteractable interactable))
                interactable.Interact();
            else
                Debug.Log($"Hit '{hit.collider.name}' — not interactable");
        }
        else
        {
            Debug.Log("Nothing to interact with");
        }
    }
}
```

**Step 2: Verify it compiles**

Check Console in Unity Editor — no errors.

**Step 3: Commit**

```bash
git add Assets/Scripts/PlayerInteract.cs Assets/Scripts/PlayerInteract.cs.meta
git commit -m "feat: add PlayerInteract with IInteractable interface and E-key raycast"
```

---

## Task 4: Unity Editor Scene Setup

> This task is done manually in the Unity Editor — no code to write.

**Step 1: Create the Platform**
1. Hierarchy → right-click → 3D Object → Cube
2. Rename to `Platform`
3. In Inspector, set Transform: Position `(0, -0.25, 0)`, Scale `(20, 0.5, 20)`
4. Box Collider is added automatically — leave as-is

**Step 2: Create the Player**
1. Hierarchy → right-click → Create Empty
2. Rename to `Player`
3. Position: `(0, 1.5, 0)`
4. Add Component → `Character Controller` (leave defaults)
5. Add Component → `Player Movement` (the script you just wrote)

**Step 3: Create the PlayerCamera**
1. In Hierarchy, right-click on `Player` → Camera (adds it as a child)
2. Rename to `PlayerCamera`
3. Local Position: `(0, 0.7, 0)` (eye height)
4. Tag: `MainCamera`
5. Add Component → `Camera Look`
6. Add Component → `Player Interact`

**Step 4: Wire Inspector references**
1. Select `PlayerCamera`
2. In `CameraLook` component, drag `Player` from Hierarchy into the `Player Body` field

**Step 5: Clean up default camera**
- If a separate `Main Camera` exists in the Hierarchy (not the one you just created), delete it

**Step 6: Confirm Directional Light exists**
- Hierarchy should have a `Directional Light` — if not, Lighting → 3D Object → Directional Light

---

## Task 5: Verification (Play Mode)

**Step 1: Press Play in Unity Editor**

**Step 2: Check each behavior**

| Test | Expected result |
|---|---|
| Player spawns | No fall-through; lands on platform |
| WASD | Player walks around the platform |
| Left Shift + WASD | Player moves noticeably faster |
| Space | Player jumps and lands cleanly |
| Mouse move | Camera rotates; cursor is hidden |
| Look straight up / down | Camera stops at ~80° (no flip) |
| Press E facing platform | Console: `Hit 'Platform' — not interactable` |
| Press E facing empty space | Console: `Nothing to interact with` |

**Step 3: If cursor stays visible**

Check `CameraLook.Start()` is running — make sure the script is on `PlayerCamera` and not disabled.

**Step 4: Final commit**

```bash
git add Assets/Scenes/SampleScene.unity
git commit -m "feat: wire first-person player and platform in SampleScene"
```

---

## File Summary

| File | Status |
|---|---|
| `Assets/Scripts/PlayerMovement.cs` | Create |
| `Assets/Scripts/CameraLook.cs` | Create |
| `Assets/Scripts/PlayerInteract.cs` | Create |
| `Assets/Scenes/SampleScene.unity` | Modified in Editor |
| `docs/plans/2026-03-08-first-person-controller.md` | Create |
| `/Users/erick/demisidian/matador/comp565/first-person-controller-plan.md` | Create |

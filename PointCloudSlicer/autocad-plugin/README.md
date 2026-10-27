# PointCloudSlicer — AutoCAD Plugin

An AutoCAD .NET plugin for slicing and clipping `.rcp` / `.rcs` point clouds by picking points on X, Y, and Z planes directly in the viewport.

---

## Commands

| Command | Description |
|---|---|
| `PCPICKX` | Pick a point → define a clipping plane perpendicular to X (YZ plane) |
| `PCPICKY` | Pick a point → define a clipping plane perpendicular to Y (XZ plane) |
| `PCPICKZ` | Pick a point → define a clipping plane perpendicular to Z (XY plane, horizontal) |
| `PCSLICE` | Apply all defined planes to a selected point cloud |
| `PCSLICEBAND` | Pick two points to define a min/max slab on X, Y, or Z, then apply |
| `PCREMOVECLIP` | Remove all clipping boundaries from a selected point cloud |
| `PCSLICERESET` | Clear stored planes from memory (does not change the drawing) |
| `PCSTATUS` | Show all currently defined planes |
| `PCEXTENTS` | Report the X/Y/Z extents of a selected point cloud |

---

## Typical Workflow

### Horizontal slice (Z — most common for floor-level sections)

1. Run `PCPICKZ` → snap to a point at the floor level → choose **Above** to keep everything above it.
2. Run `PCPICKZ` again → snap to a point at ceiling level → choose **Below** to keep everything below.
3. Run `PCSLICE` → click the point cloud → both planes are applied as a slab.

Or use the shortcut: `PCSLICEBAND` → choose **Z** → pick bottom point → pick top point → select cloud.

### Vertical wall slice (X or Y)

1. Run `PCPICKX` → snap to a wall face → choose **Positive** or **Negative** depending on which side to keep.
2. Optionally add a second X plane on the opposite wall for a corridor slice.
3. Run `PCSLICE` → select cloud.

### Combine all three axes

You can mix X, Y, and Z planes freely. Each call to `PCPICKX/Y/Z` stores one plane per axis/direction. Run `PCSTATUS` to review before applying. Run `PCSLICE` once to apply all at once.

---

## Building the Plugin

### Prerequisites

- **Visual Studio 2022** (or the .NET SDK 4.8 + MSBuild)
- **AutoCAD 2020 or later** installed (for the reference DLLs)
- .NET Framework 4.8 targeting pack

### Steps

1. Open `PointCloudSlicer.csproj` in Visual Studio.
2. Edit the `<AutoCadDir>` property to match your AutoCAD install path, e.g.:
   ```xml
   <AutoCadDir>C:\Program Files\Autodesk\AutoCAD 2025</AutoCadDir>
   ```
   Or set it at build time:
   ```
   msbuild PointCloudSlicer.csproj /p:AutoCadDir="C:\Program Files\Autodesk\AutoCAD 2024"
   ```
3. Build → **Release | x64**.
4. The output DLL is at `bin\Release\PointCloudSlicer.dll`.

### Command-line build (no Visual Studio)

```bat
dotnet build PointCloudSlicer.csproj -c Release /p:AutoCadDir="C:\Program Files\Autodesk\AutoCAD 2025"
```

> The AutoCAD reference DLLs (`acmgd.dll`, `accoremgd.dll`, `acdbmgd.dll`,
> `AcPointCloudEngineApi.dll`) are set to `Private=false` — they are **not** copied
> to the output folder. AutoCAD already loads them at runtime.

---

## Loading into AutoCAD

1. Open AutoCAD with a drawing that contains an attached `.rcp` point cloud.
2. Type `NETLOAD` in the command line.
3. Browse to `bin\Release\PointCloudSlicer.dll` and click **Open**.
4. You should see:
   ```
   PointCloudSlicer loaded. Commands: PCPICKX, PCPICKY, PCPICKZ, PCSLICE, PCSLICERESET, PCSTATUS
   ```

### Auto-load on every AutoCAD startup (optional)

Add a registry key or place `PointCloudSlicer.dll` in a folder listed under:

```
HKCU\Software\Autodesk\AutoCAD\<version>\<product>\Applications\PointCloudSlicer
```

Or add a `LISP` startup script:

```lisp
(command "NETLOAD" "C:/path/to/PointCloudSlicer.dll")
```

placed in a `acaddoc.lsp` file in your Support File Search Path.

---

## How Clipping Works

AutoCAD's `PointCloudEx` entity supports **clipping boundaries** — each boundary is a half-space defined by:

- A **plane** (origin point + normal vector)
- The normal points toward the **kept** side

This plugin stores your picked planes in memory during the session, then applies them all at once when you run `PCSLICE`. Each `PCSLICE` call **replaces** the cloud's existing clipping boundaries (no accumulation between runs).

### Side selection

When you pick a plane, you are asked which side to keep:

- `PCPICKZ` at Z = 5.0 → keep **Above** → everything with Z > 5.0 is visible
- `PCPICKZ` at Z = 10.0 → keep **Below** → everything with Z < 10.0 is visible
- Both together → slab between Z = 5.0 and Z = 10.0

For X/Y:
- keep **Positive** = keep the side where X (or Y) is greater than the picked value
- keep **Negative** = keep the side where X (or Y) is less than the picked value

---

## Version Notes

| AutoCAD | Notes |
|---|---|
| 2020 + | Full `PointCloudEx` planar clipping API — all features work |
| 2018–2019 | Plugin loads; polygon-boundary fallback is used if planar API is absent |
| < 2018 | `PointCloudEx` may not exist; use the older `PointCloud` entity path (not supported by this plugin) |

For AutoCAD 2024/2025, `AcPointCloudEngineApi.dll` is referenced and improves point cloud access performance. The plugin checks for its presence at build time.

---

## Project Structure

```
autocad-plugin/
├── PointCloudSlicer.csproj   — project file (set AutoCadDir here)
├── src/
│   ├── PluginMain.cs         — IExtensionApplication entry point
│   ├── Commands.cs           — all PCPICK*, PCSLICE*, PCSTATUS commands
│   ├── PlaneManager.cs       — session-scoped plane storage
│   └── PointCloudHelper.cs   — PointCloudEx selection, clipping, extents
└── README.md
```

---

## Troubleshooting

**`NETLOAD` says the assembly cannot be loaded**
- Make sure the DLL targets `x64` (AutoCAD is 64-bit).
- Make sure you built for .NET Framework 4.8 (not .NET 6/7/8).
- Check that the AutoCAD reference DLLs exist at the `AutoCadDir` path you provided.

**Clipping planes are applied but nothing disappears**
- The point cloud must be an `PointCloudEx` entity (attached via the Attach Point Cloud dialog from an `.rcp` or `.rcs` file). Older `PointCloud` entities (imported differently) use a different API.
- Run `PCEXTENTS` to confirm the cloud has valid geometry and that your plane values fall within the cloud bounds.

**Planes reset when I close AutoCAD**
- By design. `PlaneManager` is in-memory only. If you want persistent planes, save the plane coordinates in a drawing annotation or external text file and re-enter them on next session.

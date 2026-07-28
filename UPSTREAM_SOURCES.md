# Pinned upstream sources

The product build is pinned to the following upstream inputs. Public source
availability does not replace the license terms shipped with each component.

| Component | Version / commit | Purpose | License handling |
| --- | --- | --- | --- |
| Godot Engine | 4.7.1 stable | Application and Windows export | MIT; official binary and templates |
| spine-runtimes | `b81e5a58ed38704aee4f866f0e0ac672623ce914` on branch `4.2` | Spine 4.2 Runtime and `spine-godot` | Spine Runtimes License, shipped in full |
| godot-cpp | `eb006b6276e89fa7d7c26cc23cb3abe43e90442f` | Godot 4.7 GDExtension bindings | MIT |
| .NET | 8 | Self-contained converter runtime | Microsoft .NET license |

The official Godot 4.7.1 export-template archive was verified before template
installation with SHA-512:

`AFCC83D8D3D298038F19C58744A0D660FA75DD4BAA33CB55D1011BB2565A2A8C2381728924564CB909E37C205A23F21B521B23BD057993AFD43AE4DA0B2F9D47`

## spine-godot build compatibility patch

Apply `patches/spine-godot-4.2-godot-4.7.1.patch` at the root of the pinned
`spine-runtimes` checkout before compiling the editor, template-debug, and
template-release GDExtension binaries against the exact Godot 4.7.1 extension
API. The patch replaces obsolete raw `Object` ownership with `Ref<T>` and the
current `instantiate()` API; it does not change Spine animation behavior.

The release DLL used by `scripts/build_product.ps1` is intentionally kept in
`addons/spine_godot/windows/`. Rebuilding that DLL requires the pinned upstream
source toolchain; ordinary product packaging does not.


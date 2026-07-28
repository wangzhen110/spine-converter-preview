# Third-party notices

## Spine Runtime / spine-godot

The animation preview uses `spine-godot`, which incorporates the Esoteric
Software Spine Runtime. It is not MIT-licensed. Redistribution is governed by
the Spine Runtimes License Agreement in `SPINE-RUNTIMES-LICENSE.txt` and the
applicable Spine Editor license terms.

Under the Spine Editor License Agreement updated April 5, 2025, the publisher
must hold a valid applicable Spine Editor license when the Runtime is integrated
into each product build and must include the Spine Runtimes License Agreement
with the product. The agreement also imposes a revenue threshold for Essential
and Professional licenses. The publisher must verify its own license tier and
facts; this notice is not legal advice.

This application converts skeleton data and previews it locally. It does not
copy the Spine Runtime into the user's exported skeleton data. If this product
is extended into an SDK, game toolkit, or library that lets users create new
applications containing the Spine Runtime, those users may need their own Spine
Editor licenses under Section 2.4 of the current Editor agreement.

Official agreement: https://esotericsoftware.com/spine-editor-license

## Godot Engine

The application executable is built with Godot Engine 4.7.1, licensed under
the MIT License. The full notice is in `GODOT-LICENSE.txt`.

## SpineSkeletonDataConverter

Multi-version JSON/SKEL conversion is provided by
`wang606/SpineSkeletonDataConverter`, pinned at commit
`5ecb2139b0a1af266974f95abeec6bb8562d1249`. It is licensed under PolyForm
Noncommercial License 1.0.0. The combined application may be used, modified,
and shared only for noncommercial purposes. Attribution and the license must be
retained. See `POLYFORM-NONCOMMERCIAL-LICENSE.txt`.

## Self-developed project code

The Godot interface, project-specific integration, build scripts, and project
documentation are independently developed and released under Apache License
2.0. See `LICENSE` and `NOTICE`. Apache License 2.0 does not apply to the
third-party components listed above and does not override their restrictions.

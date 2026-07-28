# Commercial converter design

## Goal

Replace the PolyForm Noncommercial converter with independently authored code.
The first production target is Spine 3.8 JSON/SKEL to Spine 4.2 JSON/SKEL.

## Source boundary

- Format facts are taken from the public Spine JSON and binary format pages:
  - https://esotericsoftware.com/spine-json-format
  - https://esotericsoftware.com/spine-binary-format
- Test fixtures must be original minimal fixtures or assets lawfully supplied by
  the product owner.
- No source code from `wang606/SpineSkeletonDataConverter` may be copied into
  this directory.
- Spine Runtime source has a separate Spine Runtimes License. It is not copied
  into this converter. Runtime behavior may be used only as a compatibility
  test when the applicable Spine license permits it.

## Delivery stages

1. JSON 3.8 reader, normalized model, and JSON 4.2 writer.
2. Binary 3.8 reader based on the public binary format specification.
3. Binary 4.2 writer based on the public binary format specification.
4. Golden-file and runtime-loading tests across representative attachments,
   constraints, meshes, deform timelines, events, and draw order.
5. Replace the GUI process path only after the compatibility suite passes.

The current code is a development foundation, not yet a production-complete
Spine converter. Unsupported paths fail explicitly instead of emitting files
that merely claim a different Spine version.

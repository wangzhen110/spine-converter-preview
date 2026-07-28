# Validation record

## Spine 3.8 JSON to Spine 4.2 JSON

- Source corpus: 21 JSON exports from the official Spine Runtimes `3.8`
  examples branch.
- Conversion: 21 succeeded, 0 failed.
- Runtime load: 20 models with an atlas loaded in the Spine 4.2 Godot Runtime;
  1 model had no atlas in the source tree; 0 runtime failures.

## Spine 3.8 SKEL parser

- Official corpus: 21 SKEL exports read exactly to EOF, 0 failures.
- Product-owner corpus: 358 original Spine `3.8.75` SKEL files read exactly
  to EOF, 0 failures.
- Coverage in the product-owner corpus:
  - 19,426 mesh attachments
  - 10,986 weighted vertex attachments
  - 5,410 region attachments
  - 285 bounding box attachments
  - 30 clipping attachments
  - 30 path attachments
  - 372,007 animation timelines
  - 2,581,962 animation frames

## Spine 3.8 SKEL to Spine 4.2 JSON

- Official corpus: 21 conversions succeeded; all 20 models with atlases loaded
  in the Spine 4.2 Godot Runtime, 0 failures.
- Product-owner corpus: 358 conversions succeeded. All 357 models with atlases
  loaded in the Spine 4.2 Godot Runtime, 0 failures. One source directory did
  not contain an atlas and was not runtime-renderable.

These results prove the tested conversion paths for the listed corpora. They do
not prove reverse conversion or compatibility with arbitrary third-party
skeletons outside the tested format line.

## Spine 3.8 SKEL to Spine 4.2 SKEL

- Official corpus: 21 binary writes succeeded; all 20 models with atlases
  loaded in the Spine 4.2 Godot Runtime, 0 failures.
- Product-owner corpus: 358 binary writes succeeded. All 357 models with
  atlases loaded in one Spine 4.2 Godot Runtime batch, 0 failures. The same
  source directory lacking an atlas could not be rendered.
- The writer rejects unsupported attachment timelines and cannot be reused
  accidentally for a second skeleton.
- Validation found that Spine 4.2 binary derives mesh triangle count from the
  stored hull field. The writer therefore calculates that binary field from
  the actual Spine 3.8 triangle array to preserve all triangle indices.

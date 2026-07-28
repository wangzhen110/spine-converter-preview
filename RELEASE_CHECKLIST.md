# Commercial release checklist

This checklist separates reproducible technical evidence from facts that only
the publisher can establish. Checking a box is a release decision, not a legal
opinion.

## Required before a distribution build

- [ ] Record the legal person or entity that will publish the product.
- [ ] Confirm that publisher held an applicable valid Spine Editor license when
  the Runtime was integrated into this build.
- [ ] Confirm the correct license tier. Essential/Professional is subject to the
  current USD 500,000 aggregate revenue/financing threshold; Enterprise terms
  differ. Verify the current official agreement.
- [ ] Confirm the product still adds significant and primary functionality to
  the Spine Runtime and is not merely redistributing the Runtime.
- [ ] Keep `SPINE-RUNTIMES-LICENSE.txt` and all third-party notices in the package.
- [ ] If the product becomes an SDK/toolkit that creates new applications
  containing the Runtime, reassess end-user Spine licensing under Section 2.4.
- [ ] Verify that all skeletons, atlases, textures, icons, names, and marketing
  screenshots used for sale have commercial rights. Test assets are not bundled.

Official agreement: https://esotericsoftware.com/spine-editor-license

## Build and acceptance

```powershell
.\scripts\build_product.ps1 `
  -DistributionBuild `
  -SpineLicenseAcknowledged `
  -SmokeTestSource "C:\path\to\licensed-test-model.skel"
```

- [ ] Six converter regression tests pass.
- [ ] Packaged Runtime smoke test passes with no stderr errors.
- [ ] Folder import/navigation/batch export smoke test reports the expected model
  and export counts. Use `-BatchSmokeTestFolder` with at least two commercially
  licensed test models.
- [ ] `SHA256SUMS.txt` matches every bundled file.
- [ ] `BUILD-STATUS.txt` says `DISTRIBUTION BUILD`.
- [ ] Test folder import, model navigation, batch export, save-as, and default
  save directory on a clean Windows 10/11 account or VM.
- [ ] Confirm missing-atlas and invalid-input errors do not create corrupt output.
- [ ] Malware-scan the final ZIP and retain its SHA-256 with the release record.

## Publisher presentation

- [ ] Replace the generic product name/version if required.
- [ ] Add publisher/company metadata once the legal publisher name is known.
- [ ] Add a commercially licensed icon; do not reuse customer/test artwork.
- [ ] Sign the EXE/installer if a trusted code-signing certificate is available.
- [ ] Prepare support contact, refund policy, privacy statement, and product scope.
  The current application performs local file processing and does not add
  telemetry or network upload.

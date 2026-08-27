# Native codecs required for the G03 build

This directory contains the Android ARMv7/API 30 codec builds used by the G03
APK:

- `libfdk-aac.so`, exporting the `aacDecoder_*` functions used by `AACDecoder`.
- `libalac.so`, exporting `InitializeDecoder` and `Decode` as expected by `ALACDecoder`.

Rebuild them with `scripts/build-native-codecs-armv7.sh`. The script pins the
source revisions, links the C++ runtime statically, verifies ELF32/ARM and checks
all decoder symbols consumed by the managed code.

Build provenance:

- FDK-AAC commit `7c83d08002332b2730c845eec3497e6bf585dd28`;
- GiteKat LibALAC commit `bc03e0d311a61d5a14ae2a63a188bde845ec6aa3`;
- Android NDK `26.3.11579264`, target API 30, ABI `armeabi-v7a`.

Licenses are retained in `AirPlay.Android/native/licenses`. FDK-AAC includes
specific redistribution and patent-license conditions; review its complete
notice before distributing an APK outside this test environment.

Do not copy MediaTek FairPlay, TEE or device-provisioning libraries here. This
Android port is intentionally independent from the proprietary TCL stack.

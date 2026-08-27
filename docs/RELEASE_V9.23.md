# TCL AirPlay hybrid v9.23

Complete Magisk package for the TCL G03 Android 11 ARMv7 port. It preserves the
TCL interface and TV input while replacing the unavailable proprietary daemon
with the SteeBono receiver engine.

## Included

- TCL AirPlay launcher and patched `TVInputService` player;
- SteeBono Android engine 0.3.18 (`versionCode=21`);
- separate `TCL G03 Audio` and `TCL G03 Vidéo` mDNS destinations;
- PCM, AAC/AAC-ELD and ALAC playback;
- ARMv7 `libfdk-aac.so` and `libalac.so`;
- H.264 mirroring using the Realtek hardware decoder;
- IDR caching, SPS/PPS CSD, multi-NAL parsing and decoder recovery;
- centered portrait/landscape aspect transform without resizing the decoder buffer;
- automatic boot service and one-shot TCL input switching;
- notices and licenses. No media content is recorded persistently.

## Install

Install `tcl-airplay-v9.23-all-in-one-magisk.zip` from Magisk and reboot. It
upgrades the existing module id `tcl_airplay_g08_g03_minsdk30`. Disable that
module from Magisk if the target firmware is not a compatible TCL G03 Android
11 ARMv7 build.

## Asset

`tcl-airplay-v9.23-all-in-one-magisk.zip`

SHA-256:

`9e5fd6c2598b0d599848c5fe96b666df47644b69ab083ee6685f98655f8e5304`

The matching `.sha256` file is intended to be attached next to the Release
asset.

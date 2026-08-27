# TCL AirPlay hybrid v9.23

Complete Magisk package for the TCL G03 Android 11 ARMv7 port. It preserves the
TCL interface and TV input while replacing the unavailable proprietary daemon
with the SteeBono receiver engine.

> **Experimental release:** display problems still remain to be fixed, and the
> combined TCL/SteeBono interface is not finished. More work is required for
> reliable portrait/landscape rendering, source transitions and long sessions.

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

Cette version reste expérimentale : des problèmes d'affichage sont encore à
régler et l'interface doit également être terminée.

## Asset

`tcl-airplay-v9.23-all-in-one-magisk.zip`

SHA-256:

`71eedc4958103f25c4efc4ee41844c5090229b74196316b04ef4e5d9be331a5b`

The matching `.sha256` file is intended to be attached next to the Release
asset.

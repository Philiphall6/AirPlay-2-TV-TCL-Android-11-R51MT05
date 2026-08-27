# Android 11 ARMv7 port for TCL G03

## Scope

This branch wraps the SteeBono AirPlay protocol implementation in a native
.NET for Android application. It can run standalone or replace only the old
MediaTek daemon in a hybrid Magisk module. It does not use the proprietary
FairPlay, TEE, secure-storage or provisioning libraries.

Target:

- TCL BeyondTV4 / G03
- Android 11, API 30
- tested platform `R51MT05`
- tested rooted firmware `V652`
- `armeabi-v7a`

Upstream baseline:

- <https://github.com/SteeBono/airplayreceiver>
- commit `806fd39ef263a2b38bdd7c8e636a9fd804a94c4e`
- MIT license

## Added Android integration

- Apple TV-inspired dark interface with an AirPlay symbol and TV-remote focus states;
- locally persisted receiver base name with non-editable `Audio` and `Video` suffixes;
- foreground service with a persistent notification;
- boot receiver;
- Wi-Fi multicast lock for `_airplay._tcp` and `_raop._tcp` advertisements;
- `AudioTrack` PCM sink at 44.1 kHz, stereo, 16-bit;
- `MediaCodec` H.264 decoder targeting a `SurfaceView`;
- Android-safe native-library loading through `NativeLibrary`;
- deterministic target identity, control ports 5000/7000 and H.264 data port
  7100;
- cleanup for receiver, mDNS, sockets and media outputs.

## Hybrid v8 integration

`scripts/build-hybrid-v8-magisk.py` combines the test7/v7 TCL launcher and
player interface with the SteeBono ARMv7 engine. The proprietary
`com.mediatek.airplaydaemon` APK is deliberately absent.

`LegacyTclBridgeReceiver` maps the legacy TCL start/restart/stop broadcasts to
the SteeBono foreground service. Once listeners and mDNS are ready, the service
sends the legacy ready broadcasts to the TCL packages. A Magisk `service.sh`
also starts the receiver after Android finishes booting.

The generated module is:

`/srv/partage/tcl-airplay-port/artifacts/tcl-airplay-v7-ui-steebono-engine-v8-magisk.zip`

Its SHA-256 is
`f98b4053b22d3f17ed91245a4e9795c0e89ce4a2059382e914c5689199f88d0d`.

The current boundary is visual integration: the TCL shell remains present,
but decoded H.264 is rendered through the SteeBono activity SurfaceView rather
than injected into the proprietary TCL TVInputService.

## Hybrid v9.7 control-port correction

Upstream commit `806fd39` advertises `_airplay._tcp` on port 7000 but starts
only its RTSP listener on port 5000; it attempts to bind 7000 dynamically only
after receiving `SETUP`. Hybrid v9.7 runs the unchanged SteeBono RTSP/FairPlay
handler permanently on both advertised control ports and returns port 7100 as
the screen-mirroring H.264 `dataPort`. Audio continues to use UDP 7002/7003.

The automatic URL-streaming listener is disabled because upstream starts it
for non-mirroring sessions and it can otherwise claim the video data port
during an audio-only session. This revision targets RAOP audio and screen
mirroring; AirPlay URL playback remains out of scope.

## Hybrid v9 direct TVInput bridge

The v9 module patches the retained MediaTek `AirPlayTvInputService` so its
framework-provided `Surface` is sent as a Parcelable to the SteeBono process.
`TclTvSurfaceReceiver` places it in `ReceiverSurfaceRegistry`, causing the H.264
`MediaCodec` to decode directly into the TCL `TvView` surface.

The patched session no longer checks the removed daemon's private files and
does not create the proprietary video overlay. When an H.264 frame arrives
without an attached TV surface, SteeBono opens the retained TCL `TVActivity`
and retries with a five-second debounce.

Artifact:

`/srv/partage/tcl-airplay-port/artifacts/tcl-airplay-v7-ui-steebono-tvinput-v9-magisk.zip`

SHA-256:

`949bbaeb3405afebc116a07b5e5f8cecb031f03f39e070885294f1eb90f98ea1`

The APK signatures, patched DEX markers, page alignment, embedded codecs and
ZIP integrity have been verified locally. Cross-process Surface transfer and
actual rendering still require a test on the physical G03.

## Physical G03 installation result

The final installed revision is hybrid v9.3/module `versionCode=13`, Android
app `0.3.2`/`versionCode=5`. It is stored at:

`/srv/partage/tcl-airplay-port/artifacts/tcl-airplay-v7-ui-steebono-tvinput-v9.3-magisk.zip`

The first real music test exposed a locale-dependent protocol parsing bug:
AirPlay sends volume as `0.000000`, while `Decimal.Parse()` on the French TV
expected a comma. Hybrid v9.4 fixes volume and streaming start-position
parsing with `CultureInfo.InvariantCulture`. It is installed on the bedroom
TV as module `versionCode=14`, Android app `0.3.3`/`versionCode=6`:

`/srv/partage/tcl-airplay-port/artifacts/tcl-airplay-v7-ui-steebono-tvinput-v9.4-magisk.zip`

SHA-256: `391e4d120ba5ac4608809fc3c57a34dbabd52d74e13b3976138fa1634ba45eb3`.

Hybrid v9.5 splits the advertised destinations into `TCL G03 Audio`
(`_raop._tcp`, port 5000) and `TCL G03 Vidéo` (`_airplay._tcp`, TCL
TVInputService path on port 7000). A distinct locally administered device ID
is used for audio so iOS does not merge it with video. Protocol diagnostics
are available under the logcat tag `TclAirPlayProtocol`.

Installed module:

`/srv/partage/tcl-airplay-port/artifacts/tcl-airplay-v7-ui-steebono-split-av-v9.5-magisk.zip`

SHA-256: `0dc7b4bf1e3fc1c14a0173a6807309630b535298b1938f0f33aea354654aa58a`.

SHA-256:

`4a2c3f62675bc67d1d3be7298cc396b4507a3fa1dc506bb6f622b5b955c26fda`

On systemless `system_ext`, Android expects native libraries beside the APK at
`TclAirPlayReceiver/lib/arm`; the module builder now extracts every ARMv7 JNI
library there. To avoid the G03 cold-Mono foreground-service deadline, the APK
boot receiver is disabled and Magisk launches a no-display bootstrap activity
30 seconds after boot. The warmed process then starts the foreground service.

On `192.168.1.35`, the final boot showed a stable foreground process, an
`Active receiver` notification, TCP 5000 listening, no AirPlay ANR/crash, and
the patched MediaTek TV input registered by `TvInputManager`. Actual Apple
streaming and cross-process Surface rendering remain the next interactive
test.

## Native codecs

The protocol core produces PCM after decoding ALAC or AAC. Android ARMv7 builds
are now present at:

- `AirPlay.Android/native/armeabi-v7a/libfdk-aac.so`;
- `AirPlay.Android/native/armeabi-v7a/libalac.so`.

They are reproducibly built by `scripts/build-native-codecs-armv7.sh` from
pinned FDK-AAC and GiteKat LibALAC revisions. The output is ELF32/ARM for API 30,
has no `libc++_shared.so` dependency and exports the exact C ABI used by the
managed decoders. The ALAC lifecycle also uses `FinishDecoder`.

## Build

Install the .NET 8 SDK, the .NET Android workload, Android SDK platform 34 and
an Android NDK capable of building `armeabi-v7a`. Then run:

```bash
./scripts/build-android-g03.sh
```

The APK uses `minSdkVersion=30`, targets API 34 and includes only
`armeabi-v7a`.

## Validation order on the TV

1. Install and launch the APK manually.
2. Confirm the foreground notification appears.
3. Verify TCP 5000 and 7000 and UDP 7002/7003 when an audio session starts.
4. Confirm `_airplay._tcp` and `_raop._tcp` from another LAN device.
5. Attempt PCM, ALAC and AAC audio separately.
6. Open the activity before mirroring so a valid video `Surface` exists.
7. Capture `logcat -s TclAirPlay` during pairing and streaming.

## Current limitations

- A development-signed ARMv7 APK has been compiled successfully with .NET 8,
  Android workload 34, API 34 and JDK 17. Both native audio codecs are embedded,
  but actual AAC/ALAC playback still requires validation on the TCL G03.
- The remaining `curve25519-pcl` dependency is an old portable assembly and
  produces .NET compatibility warnings. It is retained because upstream uses
  its `org.whispersystems.curve25519` API during AirPlay pair verification.
- Audio timing is delegated to the upstream RAOP jitter buffer; no Android
  clock correction has been added.
- H.264 timestamps from upstream still require validation against
  `MediaCodec` microseconds.
- The upstream project was originally tested with iOS 14. Modern iOS
  compatibility needs an on-device test.

## Verified build

On 2026-08-27, both `AirPlay.Core` and `AirPlay.Android` compiled with zero
errors. The generated development APK is:

`AirPlay.Android/bin/Release/net8.0-android34.0/android-arm/com.philphall.tclairplayreceiver-Signed.apk`

SHA-256:

`b434d0d79bb8800d3780044e85866c6a9e9de2a9768f4fbc568dc17ea2369593`

A stable copy for installation tests is stored at:

`/srv/partage/tcl-airplay-port/artifacts/SteeBono-AirPlay-G03-hybrid-bridge-armv7.apk`

Embedded native-library SHA-256 values:

- `libfdk-aac.so`: `dca7e369ae9dea8e093ed755ab4c73b2b18ff9209ff7fbcb2b7dd25a95de4f34`;
- `libalac.so`: `4ccff2836102d2e6d895ef9d937e0b62e41b8478edea0847feff7ab8a48178cb`.

## Hybrid v9.17 aspect-ratio bridge

The Android engine now forwards the H.264 source dimensions to the patched TCL
TV input. `AirPlayTvInputService` reports them through
`notifyVideoSizeChanged()` so the TCL viewer can preserve the source aspect
ratio and update it after phone rotation. The broadcast contains dimensions
only and is retried every two seconds while mirroring.

Installed test versions are Android `0.3.16` (`versionCode=19`) and Magisk
module v9.17 (`versionCode=27`). The persistent archive is:

`/srv/partage/tcl-airplay-port/artifacts/tcl-airplay-v7-ui-steebono-split-av-v9.17-magisk.zip`

SHA-256: `6c262bb498da698b0da62a5e18d649bb6e4c491545109dfd81d595847d3c3edb`.

The validated successor is v9.22 with Android engine 0.3.17. It keeps the
Realtek decoder Surface at its full buffer size and applies a centered aspect
transform to the TCL `TvView` three seconds after Surface acquisition. A live
888x1920 stream rendered as 500x1080 at x=710..1210 while decoder output kept
pace with input. Persistent artifact:

`/srv/partage/tcl-airplay-port/artifacts/tcl-airplay-v7-ui-steebono-split-av-v9.22-magisk.zip`

SHA-256: `74f892b69ce0283c448c0cb295daab41af30b1017c8153320ac774421313253f`.

The current source revision is Android engine 0.3.18 (`versionCode=21`). Once
a valid TCL TV-input Surface has been observed for a mirroring session, it no
longer repeats `Start.LunaTest`; this prevents the general TCL TV activity from
replacing the direct aspect-correct AirPlay player after playback has started.

## Complete v9.23 package

The GitHub Release asset is built as
`tcl-airplay-v9.23-all-in-one-magisk.zip`. It combines the current TCL launcher,
patched player, Android engine 0.3.18, both ARMv7 audio codecs, boot service,
SELinux permission merge, documentation and licenses in one Magisk archive.
See `docs/RELEASE_V9.23.md` for the asset description and installation notes.

v9.23 is experimental. Display issues remain to be solved and the integrated
TCL/SteeBono interface is still incomplete; it must not be presented as a
finished production release.

## Version 10.0

The v10.0 Android application (`versionCode=24`) adds the new dark TV interface,
the AirPlay symbol, remote-focus states and a locally persisted receiver base
name. The advertised names always retain the fixed `Audio` and `Video` suffixes.
Saving the name restarts the foreground receiver so the new mDNS records are
published without rebooting the television.

The application was compiled with .NET 8 for Android 34 and installed on the
rooted Android TV 11 R51MT05/V652 test television. Android reports version 10.0,
the foreground service runs, and TCP control ports 5000 and 7000 are reachable.
The TCL system AirPlay source now routes manual selections to the stable v10
`MainActivity`. Internal mirroring launches carry `STEEBONO_VIDEO=true` and
continue through the retained MediaTek `TVInputService`, avoiding the TCL 904
error without replacing the direct video Surface path.

The corresponding Magisk module is v10.0 (`versionCode=36`). The boot service
enables `TclAirPlayTileAccessibilityService` while preserving existing enabled
accessibility services. The `com.tcl.airplay2` package and its `BootupReceiver`
remain enabled because the TCL tile needs both. Only its legacy `BootupService`,
which creates the error 904 dialog, is disabled. The accessibility bridge
remains available as a fallback, while the Magisk root bridge watches the exact
`Show.Home.AirplayAPK` receiver event and opens v10 directly. Screen-mirroring
rendering still needs a complete physical regression test. Module activation,
foreground-service restart and control ports 5000/7000 were verified after a
full television reboot.

Release notes: `docs/RELEASE_V10.0.md`.

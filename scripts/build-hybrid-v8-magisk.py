#!/usr/bin/env python3
"""Build a TCL v7 interface + SteeBono engine Magisk module."""

import argparse
import hashlib
import zipfile
from pathlib import Path


MODULE_ID = "tcl_airplay_g08_g03_minsdk30"
DEFAULT_ZIP_NAME = "tcl-airplay-v7-ui-steebono-engine-v8-magisk.zip"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def add_bytes(archive: zipfile.ZipFile, data: bytes, arcname: str, mode: int = 0o644) -> None:
    info = zipfile.ZipInfo(arcname, date_time=(2009, 1, 1, 0, 0, 0))
    info.compress_type = zipfile.ZIP_DEFLATED
    info.external_attr = (mode & 0xFFFF) << 16
    archive.writestr(info, data)


def add_file(archive: zipfile.ZipFile, source: Path, arcname: str, mode: int = 0o644) -> None:
    add_bytes(archive, source.read_bytes(), arcname, mode)


def add_text(archive: zipfile.ZipFile, text: str, arcname: str, mode: int = 0o644) -> None:
    add_bytes(archive, text.encode("utf-8"), arcname, mode)


def add_system_app_native_libs(archive: zipfile.ZipFile, apk: Path) -> list[str]:
    """Expose compressed APK libraries where Android loads system-app JNI libs."""
    prefix = "lib/armeabi-v7a/"
    added = []
    with zipfile.ZipFile(apk) as apk_archive:
        for name in sorted(apk_archive.namelist()):
            if not name.startswith(prefix) or not name.endswith(".so"):
                continue
            basename = Path(name).name
            add_bytes(
                archive,
                apk_archive.read(name),
                f"system/system_ext/app/TclAirPlayReceiver/lib/arm/{basename}",
            )
            added.append(basename)
    if "libmonosgen-2.0.so" not in added:
        raise SystemExit("SteeBono APK does not contain libmonosgen-2.0.so")
    return added


def require_file(path: Path) -> None:
    if not path.is_file():
        raise SystemExit(f"Missing input file: {path}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--tcl-launcher", required=True, type=Path)
    parser.add_argument("--tcl-player", required=True, type=Path)
    parser.add_argument("--steebono-apk", required=True, type=Path)
    parser.add_argument("--plat-mac", required=True, type=Path)
    parser.add_argument("--steebono-license", required=True, type=Path)
    parser.add_argument("--fdk-notice", required=True, type=Path)
    parser.add_argument("--alac-license", required=True, type=Path)
    parser.add_argument("--out-dir", required=True, type=Path)
    parser.add_argument("--zip-name", default=DEFAULT_ZIP_NAME)
    parser.add_argument("--label", default="v8")
    parser.add_argument("--module-version", default="hybrid-v8-tcl-ui-steebono-engine")
    parser.add_argument("--version-code", default=9, type=int)
    parser.add_argument("--tvinput-bridge", action="store_true")
    args = parser.parse_args()

    inputs = (
        args.tcl_launcher,
        args.tcl_player,
        args.steebono_apk,
        args.plat_mac,
        args.steebono_license,
        args.fdk_notice,
        args.alac_license,
    )
    for source in inputs:
        require_file(source)

    args.out_dir.mkdir(parents=True, exist_ok=True)
    zip_path = args.out_dir / args.zip_name
    sha_path = args.out_dir / f"{args.zip_name}.sha256"

    bridge_description = (
        " The TCL TVInputService forwards its framework Surface directly to SteeBono MediaCodec."
        if args.tvinput_bridge
        else ""
    )
    module_prop = f"""id=tcl_airplay_g08_g03_minsdk30
name=TCL AirPlay v7 UI + SteeBono Engine {args.label}
version={args.module_version}
versionCode={args.version_code}
author=philphall
description=Keeps the TCL G08 launcher/player interface on G03 Android 11 and replaces the proprietary MediaTek daemon with the ARMv7 SteeBono AirPlay receiver, codecs and mDNS service.{bridge_description}
"""

    bridge_print = (
        'ui_print "- Direct TCL TVInputService Surface bridge enabled"\n'
        if args.tvinput_bridge
        else ""
    )
    customize = f"""ui_print " "
ui_print "TCL AirPlay hybrid {args.label}"
ui_print "- TCL launcher and player interface kept from v7"
ui_print "- MediaTek AirPlayDaemon removed"
ui_print "- SteeBono ARMv7 engine installed instead"
{bridge_print}ui_print "- Automatic foreground service and mDNS at boot"
ui_print "- Disable/remove this module in Magisk if boot issues occur"
ui_print " "
set_perm_recursive "$MODPATH" 0 0 0755 0644
set_perm "$MODPATH/service.sh" 0 0 0755
"""

    service = """#!/system/bin/sh
until [ "$(getprop sys.boot_completed)" = "1" ]; do
  sleep 2
done
sleep 30
am start --user 0 -n com.philphall.tclairplayreceiver/com.philphall.tclairplayreceiver.BootstrapActivity >/dev/null 2>&1
"""

    integration = (
        """Direct TVInput integration:
- AirPlayTvInputService.onSetSurface() forwards the TvView Surface by IPC.
- SteeBono MediaCodec decodes H.264 directly into that TCL TVInput Surface.
- The proprietary-daemon readiness check and video overlay are disabled.
- The TCL TVActivity is opened automatically when the first H.264 frame arrives.
"""
        if args.tvinput_bridge
        else """Important integration boundary:
- The TCL shell remains installed and can start/stop the SteeBono service.
- Video decoded by SteeBono is currently displayed by the SteeBono SurfaceView;
  direct injection into the proprietary TCL TVInputService is not implemented.
"""
    )
    readme = f"""TCL AirPlay hybrid {args.label}: v7 interface + SteeBono engine

Kept from v7:
- com.tcl.airplay2 (TCL launcher/settings interface)
- com.mediatek.AirplayAPK (TCL/MediaTek player interface)
- tested plat_mac_permissions.xml merge

Replacement engine:
- com.philphall.tclairplayreceiver (SteeBono Android ARMv7 port)
- libfdk-aac.so and libalac.so embedded in the APK
- AirPlay/RAOP listeners on ports 7000/5000 and mDNS announcements
- compatibility receiver for TCL start, restart and stop broadcasts

Current receiver features:
- separate TCL G03 Audio and TCL G03 Video AirPlay destinations
- PCM, AAC/AAC-ELD and ALAC audio through Android AudioTrack
- H.264 mirroring through the Realtek hardware MediaCodec decoder
- exact mirroring TCP framing, multi-NAL access units and IDR/SPS/PPS recovery
- direct TCL TVInputService Surface bridge with centered portrait/landscape ratio
- delayed aspect transform so the Realtek decoder starts on a full-size buffer
- one-shot TCL source switching per session to keep the direct player visible
- no persistent media recording or H.264/audio dump

Not included:
- com.mediatek.airplaydaemon (the old proprietary engine)

Experimental status / remaining work:
- display issues remain and still need correction on the target TV
- the combined TCL/SteeBono user interface is not finished yet
- portrait, landscape, source switching and long sessions need more testing

{integration}

Module id: {MODULE_ID}
Upgrade: install over test7/v7 in Magisk, then reboot.
Recovery: disable the module in Magisk, or create
  /data/adb/modules/{MODULE_ID}/disable
and reboot.

Input SHA-256:
- TCL launcher: {sha256(args.tcl_launcher)}
- TCL player: {sha256(args.tcl_player)}
- SteeBono APK: {sha256(args.steebono_apk)}
"""

    with zipfile.ZipFile(zip_path, "w") as archive:
        add_text(archive, module_prop, "module.prop")
        add_text(archive, customize, "customize.sh", 0o755)
        add_text(archive, service, "service.sh", 0o755)
        add_text(archive, readme, "README-HYBRID.txt")
        add_file(
            archive,
            args.tcl_launcher,
            "system/system_ext/app/AirPlayLaunchService/AirPlayLaunchService.apk",
        )
        add_file(
            archive,
            args.tcl_player,
            "system/system_ext/app/AirPlay/AirPlay.apk",
        )
        add_file(
            archive,
            args.steebono_apk,
            "system/system_ext/app/TclAirPlayReceiver/TclAirPlayReceiver.apk",
        )
        add_system_app_native_libs(archive, args.steebono_apk)
        add_file(archive, args.plat_mac, "system/etc/selinux/plat_mac_permissions.xml")
        add_file(archive, args.steebono_license, "LICENSES/SteeBono-LICENSE")
        add_file(archive, args.fdk_notice, "LICENSES/FDK-AAC-NOTICE.txt")
        add_file(archive, args.alac_license, "LICENSES/LibALAC-LICENSE.txt")

    result_hash = sha256(zip_path)
    sha_path.write_text(f"{result_hash}  {args.zip_name}\n", encoding="utf-8")
    print(zip_path)
    print(sha_path)
    print(result_hash)


if __name__ == "__main__":
    main()

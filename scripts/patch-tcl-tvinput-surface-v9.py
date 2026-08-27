#!/usr/bin/env python3
"""Patch the TCL TVInputService to forward its framework Surface to SteeBono."""

import argparse
import hashlib
import os
import re
import subprocess
from pathlib import Path


SESSION_SMALI = Path(
    "smali/com/mediatek/AirplayAPK/"
    "AirPlayTvInputService$AirPlayTvInputSessionImpl.smali"
)
SERVICE_SMALI = Path("smali/com/mediatek/AirplayAPK/AirPlayTvInputService.smali")
TV_ACTIVITY_SMALI = Path("smali/com/mediatek/activity/TVActivity.smali")
GEOMETRY_RECEIVER_SMALI = Path(
    "smali/com/mediatek/AirplayAPK/SteeBonoGeometryReceiver.smali"
)

SET_SURFACE_METHOD = r'''.method public onSetSurface(Landroid/view/Surface;)Z
    .locals 3

    const-string v0, "AirPlayTIS"

    const-string v1, "onSetSurface() -> SteeBono"

    invoke-static {v0, v1}, Landroid/util/Log;->i(Ljava/lang/String;Ljava/lang/String;)I

    sget v0, Lcom/mediatek/AirplayAPK/AirPlayTvInputService;->steeBonoWidth:I

    sget v1, Lcom/mediatek/AirplayAPK/AirPlayTvInputService;->steeBonoHeight:I

    if-lez v0, :geometry_done

    if-lez v1, :geometry_done

    invoke-static {v0, v1}, Lcom/mediatek/AirplayAPK/AirPlayTvInputService;->notifySteeBonoVideoSize(II)V

    :geometry_done

    new-instance v0, Landroid/content/Intent;

    const-string v1, "com.philphall.tclairplayreceiver.TCL_TV_SURFACE"

    invoke-direct {v0, v1}, Landroid/content/Intent;-><init>(Ljava/lang/String;)V

    const-string v1, "com.philphall.tclairplayreceiver"

    invoke-virtual {v0, v1}, Landroid/content/Intent;->setPackage(Ljava/lang/String;)Landroid/content/Intent;

    const-string v1, "surface"

    invoke-virtual {v0, v1, p1}, Landroid/content/Intent;->putExtra(Ljava/lang/String;Landroid/os/Parcelable;)Landroid/content/Intent;

    iget-object v1, p0, Lcom/mediatek/AirplayAPK/AirPlayTvInputService$AirPlayTvInputSessionImpl;->this$0:Lcom/mediatek/AirplayAPK/AirPlayTvInputService;

    const-string v2, "com.mediatek.permission.AirPlay.BroadCast"

    invoke-virtual {v1, v0, v2}, Landroid/content/Context;->sendBroadcast(Landroid/content/Intent;Ljava/lang/String;)V

    const/4 v0, 0x1

    return v0
.end method'''

CREATE_SESSION_METHOD = r'''.method public final onCreateSession(Ljava/lang/String;)Landroid/media/tv/TvInputService$Session;
    .locals 2

    const-string v0, "AirPlayTIS"

    const-string v1, "onCreateSession SteeBono surface bridge"

    invoke-static {v0, v1}, Landroid/util/Log;->i(Ljava/lang/String;Ljava/lang/String;)I

    new-instance v0, Lcom/mediatek/AirplayAPK/AirPlayTvInputService$AirPlayTvInputSessionImpl;

    invoke-direct {v0, p0, p0, p1}, Lcom/mediatek/AirplayAPK/AirPlayTvInputService$AirPlayTvInputSessionImpl;-><init>(Lcom/mediatek/AirplayAPK/AirPlayTvInputService;Landroid/content/Context;Ljava/lang/String;)V

    iput-object v0, p0, Lcom/mediatek/AirplayAPK/AirPlayTvInputService;->session:Lcom/mediatek/AirplayAPK/AirPlayTvInputService$AirPlayTvInputSessionImpl;

    sput-object v0, Lcom/mediatek/AirplayAPK/AirPlayTvInputService;->steeBonoSession:Lcom/mediatek/AirplayAPK/AirPlayTvInputService$AirPlayTvInputSessionImpl;

    const/4 v1, 0x0

    invoke-virtual {v0, v1}, Landroid/media/tv/TvInputService$Session;->setOverlayViewEnabled(Z)V

    return-object v0
.end method'''

VIDEO_SIZE_METHOD = r'''
.method public static notifySteeBonoVideoSize(II)V
    .locals 3

    sput p0, Lcom/mediatek/AirplayAPK/AirPlayTvInputService;->steeBonoWidth:I

    sput p1, Lcom/mediatek/AirplayAPK/AirPlayTvInputService;->steeBonoHeight:I

    invoke-static {p0, p1}, Lcom/mediatek/activity/TVActivity;->applySteeBonoAspect(II)V

    sget-object v0, Lcom/mediatek/AirplayAPK/AirPlayTvInputService;->steeBonoSession:Lcom/mediatek/AirplayAPK/AirPlayTvInputService$AirPlayTvInputSessionImpl;

    if-eqz v0, :log_size

    invoke-virtual {v0}, Landroid/media/tv/TvInputService$Session;->notifyVideoAvailable()V

    :log_size

    const-string v1, "AirPlayTIS"

    new-instance v2, Ljava/lang/StringBuilder;

    invoke-direct {v2}, Ljava/lang/StringBuilder;-><init>()V

    const-string v0, "SteeBono video size "

    invoke-virtual {v2, v0}, Ljava/lang/StringBuilder;->append(Ljava/lang/String;)Ljava/lang/StringBuilder;

    invoke-virtual {v2, p0}, Ljava/lang/StringBuilder;->append(I)Ljava/lang/StringBuilder;

    const-string v0, "x"

    invoke-virtual {v2, v0}, Ljava/lang/StringBuilder;->append(Ljava/lang/String;)Ljava/lang/StringBuilder;

    invoke-virtual {v2, p1}, Ljava/lang/StringBuilder;->append(I)Ljava/lang/StringBuilder;

    invoke-virtual {v2}, Ljava/lang/StringBuilder;->toString()Ljava/lang/String;

    move-result-object v0

    invoke-static {v1, v0}, Landroid/util/Log;->i(Ljava/lang/String;Ljava/lang/String;)I

    :done
    return-void
.end method
'''

TV_ACTIVITY_ASPECT_METHOD = r'''
.method public static applySteeBonoAspect(II)V
    .locals 7

    sget-object v0, Lcom/mediatek/activity/TVActivity;->steeBonoActivity:Lcom/mediatek/activity/TVActivity;

    if-eqz v0, :done

    iget-object v0, v0, Lcom/mediatek/activity/TVActivity;->m:Landroid/media/tv/TvView;

    if-eqz v0, :done

    const v1, 0x44700000

    invoke-virtual {v0, v1}, Landroid/view/View;->setPivotX(F)V

    const v1, 0x44070000

    invoke-virtual {v0, v1}, Landroid/view/View;->setPivotY(F)V

    const/16 v1, 0x780

    const/16 v2, 0x438

    mul-int v3, p0, v2

    mul-int v4, p1, v1

    if-le v3, v4, :fit_height

    int-to-float v3, v4

    int-to-float v4, p0

    div-float/2addr v3, v4

    int-to-float v4, v2

    div-float/2addr v3, v4

    const/high16 v4, 0x3f800000

    invoke-virtual {v0, v4}, Landroid/view/View;->setScaleX(F)V

    invoke-virtual {v0, v3}, Landroid/view/View;->setScaleY(F)V

    goto :done

    :fit_height
    int-to-float v3, v3

    int-to-float v4, p1

    div-float/2addr v3, v4

    int-to-float v4, v1

    div-float/2addr v3, v4

    const/high16 v4, 0x3f800000

    invoke-virtual {v0, v3}, Landroid/view/View;->setScaleX(F)V

    invoke-virtual {v0, v4}, Landroid/view/View;->setScaleY(F)V

    :done
    return-void
.end method
'''

TV_ACTIVITY_MENU_ROUTE = r'''
    invoke-virtual {p0}, Landroid/app/Activity;->getIntent()Landroid/content/Intent;

    move-result-object v0

    const-string v1, "com.philphall.tclairplayreceiver.STEEBONO_VIDEO"

    const/4 v2, 0x0

    invoke-virtual {v0, v1, v2}, Landroid/content/Intent;->getBooleanExtra(Ljava/lang/String;Z)Z

    move-result v0

    if-nez v0, :steebono_video_entry

    new-instance v0, Landroid/content/Intent;

    invoke-direct {v0}, Landroid/content/Intent;-><init>()V

    const-string v1, "com.philphall.tclairplayreceiver"

    const-string v2, "com.philphall.tclairplayreceiver.MainActivity"

    invoke-virtual {v0, v1, v2}, Landroid/content/Intent;->setClassName(Ljava/lang/String;Ljava/lang/String;)Landroid/content/Intent;

    const/high16 v1, 0x10000000

    invoke-virtual {v0, v1}, Landroid/content/Intent;->addFlags(I)Landroid/content/Intent;

    invoke-virtual {p0, v0}, Landroid/content/Context;->startActivity(Landroid/content/Intent;)V

    invoke-virtual {p0}, Landroid/app/Activity;->finish()V

    return-void

    :steebono_video_entry
'''

GEOMETRY_RECEIVER = r'''.class public Lcom/mediatek/AirplayAPK/SteeBonoGeometryReceiver;
.super Landroid/content/BroadcastReceiver;
.source "SteeBonoGeometryReceiver.java"

.method public constructor <init>()V
    .locals 0

    invoke-direct {p0}, Landroid/content/BroadcastReceiver;-><init>()V

    return-void
.end method

.method public onReceive(Landroid/content/Context;Landroid/content/Intent;)V
    .locals 2

    const-string v0, "width"

    const/4 v1, 0x0

    invoke-virtual {p2, v0, v1}, Landroid/content/Intent;->getIntExtra(Ljava/lang/String;I)I

    move-result v0

    const-string v1, "height"

    const/4 p0, 0x0

    invoke-virtual {p2, v1, p0}, Landroid/content/Intent;->getIntExtra(Ljava/lang/String;I)I

    move-result v1

    if-lez v0, :done

    if-lez v1, :done

    invoke-static {v0, v1}, Lcom/mediatek/AirplayAPK/AirPlayTvInputService;->notifySteeBonoVideoSize(II)V

    :done
    return-void
.end method
'''


def run(*command: str) -> None:
    subprocess.run(command, check=True)


def replace_method(path: Path, signature: str, replacement: str) -> None:
    text = path.read_text(encoding="utf-8")
    pattern = rf"(?ms)^\.method {re.escape(signature)}\n.*?^\.end method"
    updated, count = re.subn(pattern, replacement, text)
    if count != 1:
        raise SystemExit(f"Expected one {signature} in {path}, found {count}")
    path.write_text(updated, encoding="utf-8")


def add_geometry_bridge(decoded: Path) -> None:
    service_path = decoded / SERVICE_SMALI
    service = service_path.read_text(encoding="utf-8")
    static_marker = "# static fields\n"
    if ".field public static steeBonoSession:" not in service:
        service = service.replace(
            static_marker,
            static_marker
            + ".field public static steeBonoSession:Lcom/mediatek/AirplayAPK/"
            + "AirPlayTvInputService$AirPlayTvInputSessionImpl;\n\n",
            1,
        )
    if ".field public static steeBonoWidth:I" not in service:
        service = service.replace(
            static_marker,
            static_marker
            + ".field public static steeBonoWidth:I\n\n"
            + ".field public static steeBonoHeight:I\n\n",
            1,
        )
    if "notifySteeBonoVideoSize(II)V" not in service:
        service = service.rstrip() + "\n\n" + VIDEO_SIZE_METHOD.strip() + "\n"
    service_path.write_text(service, encoding="utf-8")

    receiver_path = decoded / GEOMETRY_RECEIVER_SMALI
    receiver_path.parent.mkdir(parents=True, exist_ok=True)
    receiver_path.write_text(GEOMETRY_RECEIVER, encoding="utf-8")

    activity_path = decoded / TV_ACTIVITY_SMALI
    activity = activity_path.read_text(encoding="utf-8")
    if ".field public static steeBonoActivity:" not in activity:
        activity = activity.replace(
            "# instance fields\n",
            "# static fields\n.field public static steeBonoActivity:Lcom/mediatek/activity/TVActivity;\n\n# instance fields\n",
            1,
        )
    on_create_super = "    invoke-super {p0, p1}, Landroidx/fragment/app/FragmentActivity;->onCreate(Landroid/os/Bundle;)V\n"
    if "sput-object p0, Lcom/mediatek/activity/TVActivity;->steeBonoActivity" not in activity:
        activity = activity.replace(
            on_create_super,
            on_create_super
            + "\n    sput-object p0, Lcom/mediatek/activity/TVActivity;->steeBonoActivity:Lcom/mediatek/activity/TVActivity;\n",
            1,
        )
    if "applySteeBonoAspect(II)V" not in activity:
        activity = activity.rstrip() + "\n\n" + TV_ACTIVITY_ASPECT_METHOD.strip() + "\n"
    activity_path.write_text(activity, encoding="utf-8")

    manifest_path = decoded / "AndroidManifest.xml"
    manifest = manifest_path.read_text(encoding="utf-8")
    receiver = '''        <receiver android:exported="true" android:name="com.mediatek.AirplayAPK.SteeBonoGeometryReceiver" android:permission="com.mediatek.permission.AirPlay.BroadCast">\n            <intent-filter>\n                <action android:name="com.philphall.tclairplayreceiver.TCL_VIDEO_GEOMETRY"/>\n            </intent-filter>\n        </receiver>\n'''
    if "SteeBonoGeometryReceiver" not in manifest:
        manifest = manifest.replace("    </application>", receiver + "    </application>", 1)
    manifest_path.write_text(manifest, encoding="utf-8")


def add_tcl_menu_route(decoded: Path) -> None:
    """Route a manual TCL AirPlay source selection to the v10 application.

    SteeBono's own video launch carries STEEBONO_VIDEO=true and therefore keeps
    the retained TVInput activity and Surface bridge unchanged.
    """
    activity_path = decoded / TV_ACTIVITY_SMALI
    activity = activity_path.read_text(encoding="utf-8")
    if "com.philphall.tclairplayreceiver.STEEBONO_VIDEO" in activity:
        return
    on_create_super = (
        "    invoke-super {p0, p1}, "
        "Landroidx/fragment/app/FragmentActivity;->onCreate(Landroid/os/Bundle;)V\n"
    )
    if on_create_super not in activity:
        raise SystemExit(f"TVActivity onCreate marker not found in {activity_path}")
    activity = activity.replace(
        on_create_super,
        on_create_super + "\n" + TV_ACTIVITY_MENU_ROUTE.strip("\n") + "\n",
        1,
    )
    activity_path.write_text(activity, encoding="utf-8")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--java", required=True, type=Path)
    parser.add_argument("--apktool", required=True, type=Path)
    parser.add_argument("--zipalign", required=True, type=Path)
    parser.add_argument("--apksigner", required=True, type=Path)
    parser.add_argument("--openssl", required=True, type=Path)
    parser.add_argument("--input-apk", required=True, type=Path)
    parser.add_argument("--key", required=True, type=Path)
    parser.add_argument("--cert", required=True, type=Path)
    parser.add_argument("--work-dir", required=True, type=Path)
    parser.add_argument("--output-apk", required=True, type=Path)
    args = parser.parse_args()

    for path in (
        args.java,
        args.apktool,
        args.zipalign,
        args.apksigner,
        args.openssl,
        args.input_apk,
        args.key,
        args.cert,
    ):
        if not path.is_file():
            raise SystemExit(f"Missing input: {path}")
    os.environ["PATH"] = str(args.java.parent) + os.pathsep + os.environ.get("PATH", "")
    if args.work_dir.exists():
        raise SystemExit(f"Work directory already exists: {args.work_dir}")

    decoded = args.work_dir / "decoded"
    unsigned = args.work_dir / "player-unsigned.apk"
    aligned = args.work_dir / "player-aligned.apk"
    signing_key = args.key
    args.work_dir.mkdir(parents=True)
    args.output_apk.parent.mkdir(parents=True, exist_ok=True)

    run(str(args.java), "-jar", str(args.apktool), "d", "-f", "-o", str(decoded), str(args.input_apk))
    replace_method(
        decoded / SESSION_SMALI,
        "public onSetSurface(Landroid/view/Surface;)Z",
        SET_SURFACE_METHOD,
    )
    replace_method(
        decoded / SERVICE_SMALI,
        "public final onCreateSession(Ljava/lang/String;)Landroid/media/tv/TvInputService$Session;",
        CREATE_SESSION_METHOD,
    )
    add_geometry_bridge(decoded)
    add_tcl_menu_route(decoded)
    run(str(args.java), "-jar", str(args.apktool), "b", "-o", str(unsigned), str(decoded))
    run(str(args.zipalign), "-p", "-f", "4", str(unsigned), str(aligned))
    if args.key.read_bytes().startswith(b"-----BEGIN"):
        signing_key = args.work_dir / "signing-key.pk8"
        run(
            str(args.openssl),
            "pkcs8",
            "-topk8",
            "-nocrypt",
            "-in",
            str(args.key),
            "-outform",
            "DER",
            "-out",
            str(signing_key),
        )
    run(
        str(args.apksigner),
        "sign",
        "--key",
        str(signing_key),
        "--cert",
        str(args.cert),
        "--out",
        str(args.output_apk),
        str(aligned),
    )
    run(str(args.apksigner), "verify", "--verbose", str(args.output_apk))
    print(args.output_apk)
    print(sha256(args.output_apk))


if __name__ == "__main__":
    main()

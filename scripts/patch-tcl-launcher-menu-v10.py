#!/usr/bin/env python3
"""Patch the TCL AirPlay launcher so its system tile opens the v10 app."""

import argparse
import hashlib
import os
import re
import subprocess
from pathlib import Path


RECEIVER = Path("smali/com/mediatek/partner/airplay/BootupReceiver.smali")

ON_RECEIVE = r'''.method public onReceive(Landroid/content/Context;Landroid/content/Intent;)V
    .locals 3

    invoke-virtual {p2}, Landroid/content/Intent;->getAction()Ljava/lang/String;

    move-result-object v0

    const-string v1, "Show.Home.AirplayAPK"

    invoke-virtual {v1, v0}, Ljava/lang/String;->equals(Ljava/lang/Object;)Z

    move-result v1

    if-eqz v1, :legacy_action

    new-instance v0, Landroid/content/Intent;

    invoke-direct {v0}, Landroid/content/Intent;-><init>()V

    const-string v1, "com.philphall.tclairplayreceiver"

    const-string v2, "com.philphall.tclairplayreceiver.MainActivity"

    invoke-virtual {v0, v1, v2}, Landroid/content/Intent;->setClassName(Ljava/lang/String;Ljava/lang/String;)Landroid/content/Intent;

    const/high16 v1, 0x10000000

    invoke-virtual {v0, v1}, Landroid/content/Intent;->addFlags(I)Landroid/content/Intent;

    invoke-virtual {p1, v0}, Landroid/content/Context;->startActivity(Landroid/content/Intent;)V

    return-void

    :legacy_action
    invoke-static {v0}, Landroid/text/TextUtils;->isEmpty(Ljava/lang/CharSequence;)Z

    move-result v1

    if-nez v1, :done

    invoke-static {}, Lcom/mediatek/partner/airplay/message/MessageHandleFactory;->getInstance()Lcom/mediatek/partner/airplay/message/MessageHandleFactory;

    move-result-object v1

    invoke-virtual {v1, v0}, Lcom/mediatek/partner/airplay/message/MessageHandleFactory;->handleRemoteMessage(Ljava/lang/String;)V

    :done
    return-void
.end method'''


def run(*command: str) -> None:
    subprocess.run(command, check=True)


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

    for source in (args.java, args.apktool, args.zipalign, args.apksigner,
                   args.openssl, args.input_apk, args.key, args.cert):
        if not source.is_file():
            raise SystemExit(f"Missing input: {source}")
    if args.work_dir.exists():
        raise SystemExit(f"Work directory already exists: {args.work_dir}")

    os.environ["PATH"] = str(args.java.parent) + os.pathsep + os.environ.get("PATH", "")
    decoded = args.work_dir / "decoded"
    unsigned = args.work_dir / "launcher-unsigned.apk"
    aligned = args.work_dir / "launcher-aligned.apk"
    args.work_dir.mkdir(parents=True)
    args.output_apk.parent.mkdir(parents=True, exist_ok=True)

    run(str(args.java), "-jar", str(args.apktool), "d", "-f", "-o", str(decoded), str(args.input_apk))
    receiver = decoded / RECEIVER
    text = receiver.read_text(encoding="utf-8")
    pattern = r"(?ms)^\.method public onReceive\(Landroid/content/Context;Landroid/content/Intent;\)V\n.*?^\.end method"
    text, count = re.subn(pattern, ON_RECEIVE, text)
    if count != 1:
        raise SystemExit(f"Expected one BootupReceiver.onReceive, found {count}")
    receiver.write_text(text, encoding="utf-8")

    run(str(args.java), "-jar", str(args.apktool), "b", "-o", str(unsigned), str(decoded))
    run(str(args.zipalign), "-p", "-f", "4", str(unsigned), str(aligned))
    signing_key = args.key
    if args.key.read_bytes().startswith(b"-----BEGIN"):
        signing_key = args.work_dir / "signing-key.pk8"
        run(str(args.openssl), "pkcs8", "-topk8", "-nocrypt", "-in", str(args.key),
            "-outform", "DER", "-out", str(signing_key))
    run(str(args.apksigner), "sign", "--key", str(signing_key), "--cert", str(args.cert),
        "--out", str(args.output_apk), str(aligned))
    run(str(args.apksigner), "verify", "--verbose", str(args.output_apk))
    print(args.output_apk)
    print(sha256(args.output_apk))


if __name__ == "__main__":
    main()

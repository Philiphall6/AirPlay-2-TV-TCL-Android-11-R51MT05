#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
android_project="$project_root/AirPlay.Android/AirPlay.Android.csproj"
native_dir="$project_root/AirPlay.Android/native/armeabi-v7a"
toolchain_root="$project_root/../toolchains"

if ! command -v dotnet >/dev/null 2>&1 && [ -x "$toolchain_root/dotnet/dotnet" ]; then
  export DOTNET_ROOT="$toolchain_root/dotnet"
  export PATH="$DOTNET_ROOT:$PATH"
  export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK is required (recommended: .NET 8 SDK)." >&2
  exit 1
fi

if ! dotnet workload list 2>/dev/null | grep -qi android; then
  echo "The Android workload is missing. Run: dotnet workload install android" >&2
  exit 1
fi

for codec in libfdk-aac.so libalac.so; do
  if [ ! -f "$native_dir/$codec" ]; then
    echo "WARNING: $native_dir/$codec is absent; AAC/ALAC playback will not work." >&2
  fi
done

export DOTNET_CLI_HOME="$project_root/.dotnet-home"
mkdir -p "$DOTNET_CLI_HOME"

android_properties=()
if [ -d "$toolchain_root/android-sdk" ]; then
  android_properties+=("-p:AndroidSdkDirectory=$toolchain_root/android-sdk")
fi
if [ -d "$toolchain_root/jdk" ]; then
  android_properties+=("-p:JavaSdkDirectory=$toolchain_root/jdk")
  export PATH="$toolchain_root/jdk/bin:$PATH"
fi

dotnet restore "$android_project" "${android_properties[@]}"
dotnet publish "$android_project" \
  --configuration Release \
  --framework net8.0-android34.0 \
  --runtime android-arm \
  --no-restore \
  "${android_properties[@]}"

find "$project_root/AirPlay.Android/bin/Release" -type f -name '*.apk' -print -exec sha256sum {} \;

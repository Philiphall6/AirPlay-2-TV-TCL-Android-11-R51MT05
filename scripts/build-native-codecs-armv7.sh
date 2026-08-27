#!/usr/bin/env bash
set -euo pipefail

fdk_commit="7c83d08002332b2730c845eec3497e6bf585dd28"
alac_commit="bc03e0d311a61d5a14ae2a63a188bde845ec6aa3"

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output_dir="$project_root/AirPlay.Android/native/armeabi-v7a"
license_dir="$project_root/AirPlay.Android/native/licenses"
build_root="${CODEC_BUILD_ROOT:-$project_root/.native-codec-build}"
source_root="$build_root/sources"
ndk_root="${ANDROID_NDK_HOME:-${ANDROID_NDK_ROOT:-}}"
sdk_root="${ANDROID_SDK_ROOT:-}"

if [ -z "$ndk_root" ] || [ ! -f "$ndk_root/build/cmake/android.toolchain.cmake" ]; then
  echo "Set ANDROID_NDK_HOME to an Android NDK (tested: 26.3.11579264)." >&2
  exit 1
fi

cmake_program="$(command -v cmake || true)"
ninja_program="$(command -v ninja || true)"
if [ -z "$cmake_program" ] && [ -n "$sdk_root" ]; then
  cmake_program="$sdk_root/cmake/3.22.1/bin/cmake"
fi
if [ -z "$ninja_program" ] && [ -n "$sdk_root" ]; then
  ninja_program="$sdk_root/cmake/3.22.1/bin/ninja"
fi
if [ ! -x "$cmake_program" ] || [ ! -x "$ninja_program" ]; then
  echo "CMake and Ninja are required (tested: Android SDK CMake 3.22.1)." >&2
  exit 1
fi

tool_bin="$ndk_root/toolchains/llvm/prebuilt/linux-x86_64/bin"
cc="$tool_bin/armv7a-linux-androideabi30-clang"
cxx="$tool_bin/armv7a-linux-androideabi30-clang++"
readelf_program="$tool_bin/llvm-readelf"
nm_program="$tool_bin/llvm-nm"
for program in "$cc" "$cxx" "$readelf_program" "$nm_program"; do
  if [ ! -x "$program" ]; then
    echo "Required NDK tool is missing: $program" >&2
    exit 1
  fi
done

mkdir -p "$source_root" "$build_root/fdk" "$build_root/alac-obj" "$output_dir" "$license_dir"

fetch_source() {
  local url="$1"
  local commit="$2"
  local destination="$3"
  if [ ! -d "$destination/.git" ]; then
    git clone --no-checkout "$url" "$destination"
  fi
  git -C "$destination" fetch --depth 1 origin "$commit"
  git -C "$destination" checkout --detach FETCH_HEAD
}

fetch_source https://github.com/mstorsjo/fdk-aac.git "$fdk_commit" "$source_root/fdk-aac"
fetch_source https://github.com/GiteKat/LibALAC.git "$alac_commit" "$source_root/libalac"

"$cmake_program" \
  -S "$source_root/fdk-aac" \
  -B "$build_root/fdk" \
  -G Ninja \
  -DCMAKE_MAKE_PROGRAM="$ninja_program" \
  -DCMAKE_TOOLCHAIN_FILE="$ndk_root/build/cmake/android.toolchain.cmake" \
  -DANDROID_ABI=armeabi-v7a \
  -DANDROID_PLATFORM=android-30 \
  -DANDROID_STL=c++_static \
  -DCMAKE_BUILD_TYPE=Release \
  -DBUILD_SHARED_LIBS=ON \
  -DBUILD_PROGRAMS=OFF \
  -DFDK_AAC_INSTALL_CMAKE_CONFIG_MODULE=OFF \
  -DFDK_AAC_INSTALL_PKGCONFIG_MODULE=OFF
"$cmake_program" --build "$build_root/fdk" --parallel

# CMake's FDK target still adds -lc++ on some toolchain combinations. Relink
# the already-built objects explicitly so the APK does not need libc++_shared.so.
shopt -s globstar nullglob
fdk_objects=("$build_root"/fdk/CMakeFiles/fdk-aac.dir/**/*.o)
"$cxx" -shared -static-libstdc++ "${fdk_objects[@]}" \
  -Wl,-soname,libfdk-aac.so -lm -latomic -o "$output_dir/libfdk-aac.so"

alac_source="$source_root/libalac/LibALAC"
c_sources=(EndianPortable.c ALACBitUtilities.c ag_dec.c ag_enc.c dp_dec.c dp_enc.c matrix_dec.c matrix_enc.c)
cpp_sources=(ALACDecoder.cpp ALACEncoder.cpp LibALAC.cpp)
for source in "${c_sources[@]}"; do
  "$cc" -c -fPIC -O2 -I"$alac_source" "$alac_source/$source" \
    -o "$build_root/alac-obj/${source%.c}.o"
done
for source in "${cpp_sources[@]}"; do
  "$cxx" -c -fPIC -O2 -fdeclspec -DLIBALAC_EXPORTS -I"$alac_source" \
    "$alac_source/$source" -o "$build_root/alac-obj/${source%.cpp}.o"
done
"$cxx" -shared -static-libstdc++ "$build_root"/alac-obj/*.o \
  -Wl,-soname,libalac.so -o "$output_dir/libalac.so"

check_library() {
  local library="$1"
  local exported_symbols
  shift
  "$readelf_program" -h "$library" | grep -q 'Class:.*ELF32'
  "$readelf_program" -h "$library" | grep -q 'Machine:.*ARM'
  if "$readelf_program" -d "$library" | grep -q 'libc++_shared.so'; then
    echo "Unexpected shared C++ runtime dependency in $library" >&2
    exit 1
  fi
  exported_symbols="$("$nm_program" -D --defined-only "$library")"
  for symbol in "$@"; do
    grep -q " $symbol$" <<< "$exported_symbols"
  done
}

check_library "$output_dir/libfdk-aac.so" \
  aacDecoder_Open aacDecoder_ConfigRaw aacDecoder_Fill aacDecoder_DecodeFrame aacDecoder_Close
check_library "$output_dir/libalac.so" InitializeDecoder Decode FinishDecoder

cp -f "$source_root/fdk-aac/NOTICE" "$license_dir/FDK-AAC-NOTICE.txt"
cp -f "$source_root/libalac/LICENSE" "$license_dir/LibALAC-LICENSE.txt"

sha256sum "$output_dir/libfdk-aac.so" "$output_dir/libalac.so"

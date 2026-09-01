# Grove Swift Video Converter

A free, portable video converter powered by FFmpeg, featuring drag-and-drop conversion, quality presets, precision trimming, visual cropping, batch processing and broad format support.

> **Current status:** Windows pre-release. A macOS edition is planned.

## Features

- Drag and drop multiple videos into a conversion queue
- Convert to MP4, MKV, WebM, MOV, AVI, GIF and MP3
- High-quality, balanced and smaller-file presets
- Preview compatible videos before conversion
- Automatic FFmpeg preview fallback for less common codecs
- Set precise start and end points for trimming
- Step the paused position by one millisecond
- Crop visually using adjustable corner and side handles
- Preserve original files and avoid overwriting existing output
- Run as a portable application without installation
- Optional Windows MSI installer

## Supported input formats

FFmpeg provides broad input support, including common MP4, MKV, MOV, AVI, WebM, WMV, M4V, MPEG, MTS, TS and FLV files.

## Repository layout

- `Windows/` — Windows application source and build scripts
- `Windows/Installer/` — WiX source for the MSI installer
- `MacOS/` — reserved for the planned macOS edition

## Building the Windows application

The current Windows build uses the Windows .NET Framework compiler and WPF.

1. Place matching `ffmpeg.exe` and `ffprobe.exe` files in `Windows/Grove Swift Video Converter/tools/`.
2. Run `Windows/build.ps1` to compile the application.
3. Run `Windows/release.ps1` to create the portable ZIP and MSI after placing the portable WiX 3.14 tools in `Windows/.tools/wix314/`.

FFmpeg and WiX binaries are intentionally excluded from this source repository.

## Downloads

Public downloads will be published on the repository's **Releases** page after FFmpeg attribution and corresponding-source materials are finalized.

## Third-party software

Grove Swift Video Converter invokes FFmpeg and FFprobe as separate command-line programs. FFmpeg is a separate open-source project and is distributed under its applicable licence. This project is not affiliated with the FFmpeg project.

## Licence

A licence for the Grove Swift Video Converter source code will be selected before the first public release.

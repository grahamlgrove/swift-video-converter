GROVE SWIFT VIDEO CONVERTER
===========================

Run GroveSwiftVideoConverter.exe from the "Grove Swift Video Converter" folder. No installation is required.

USING THE APP
1. Drag one or more videos onto the window, or click Add videos.
   Hold Ctrl or Shift to select multiple queued videos. Right-click the selection and choose Remove selected videos to remove them from the queue.
2. Select a video to preview it.
3. Use Play and the timeline to inspect it. Drag the green START and orange END calipers in the preview, use the side sliders, or use the current-position buttons below the preview. When paused, use −1 ms and +1 ms for precise positioning.
4. Drag any white handle around the green crop box. It starts at the full video frame. Click Reset crop to restore it.
5. Choose an output format and quality, then click Convert all.

By default, each output file is saved in the same folder as its original video. You can choose another folder.

The app keeps the original files unchanged. FFmpeg and FFprobe are bundled in the tools folder and must remain beside the app.

PREVIEW NOTE
Standard H.264 MP4 files open directly. For HEVC/H.265, AV1, 10-bit, unusual pixel formats and other containers, the app automatically creates and caches a Windows-compatible preview using FFmpeg. The original video is never changed.
If a compatible preview cannot be prepared within 10 seconds, the attempt is stopped and the app reports that preview is unavailable. The video can still be converted normally.

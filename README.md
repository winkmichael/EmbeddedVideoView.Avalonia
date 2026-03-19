# Avalonia.EmbeddedVideoView

Drop-in replacement for LibVLCSharp.Avalonia's `VideoView` that fixes the native window pop-out issue on Linux.

## The Problem

The official `LibVLCSharp.Avalonia` `VideoView` creates a separate floating top-level X11 window for video rendering on Linux. This window is positioned to overlap the Avalonia control, but it's not a child of the application window. During layout changes (grid resizing, camera rotation in a multi-view app), the VLC window can:

- Pop out of its grid cell and appear as a separate window
- Show a VLC-labeled window floating over your application
- Appear at incorrect coordinates, especially on 4K displays
- Flash at the stream's native resolution before being repositioned

This is a known limitation of the official library's "airspace" approach and has been [discussed extensively](https://code.videolan.org/videolan/LibVLCSharp/-/issues/475) in the LibVLCSharp issue tracker.

## The Fix

`EmbeddedVideoView` extends Avalonia's `NativeControlHost` instead of using a floating window overlay. VLC's rendering surface becomes a child window of the Avalonia host — it physically cannot appear outside its parent bounds.

```
Official VideoView:
  [Avalonia Window]  ←→  [Floating VLC X11 Window]  ← can pop out!

EmbeddedVideoView:
  [Avalonia Window]
    └── [Child VLC X11 Window]  ← constrained by parent
```

## Usage

### 1. Add the file

Copy `EmbeddedVideoView.cs` into your project. It has no dependencies beyond `LibVLCSharp` and `Avalonia` (no System.Reactive needed).

### 2. Update your AXAML

```xml
<!-- Before -->
<vlc:VideoView x:Name="VideoView"
               HorizontalAlignment="Stretch"
               VerticalAlignment="Stretch">
    <Border Background="#01000000" ... />
</vlc:VideoView>

<!-- After -->
<local:EmbeddedVideoView x:Name="VideoView"
                          HorizontalAlignment="Stretch"
                          VerticalAlignment="Stretch" />
```

Remove the transparent Border overlay — it's not needed (and was part of the airspace workaround).

### 3. Attach your MediaPlayer

```csharp
VideoView.MediaPlayer = _mediaPlayer;
```

### 4. Stretch-to-fill (optional)

If you want the video to stretch to fill the control (ignoring aspect ratio), set `AspectRatio` in the `Playing` callback **only** — not before playback starts:

```csharp
_mediaPlayer.Playing += (s, e) =>
{
    Dispatcher.UIThread.InvokeAsync(() =>
    {
        if (VideoView.Bounds.Width > 0 && VideoView.Bounds.Height > 0)
            _mediaPlayer.AspectRatio = $"{(int)VideoView.Bounds.Width}:{(int)VideoView.Bounds.Height}";
    });
};
```

**Important:** Do NOT pre-set `AspectRatio` before playback. It causes VLC to create a secondary output window alongside the embedded one.

## What you lose

- **Click/touch passthrough on video**: The official `VideoView` overlay Border allowed pointer events to bubble through. `EmbeddedVideoView` doesn't have this — VLC's native window captures input. If you need clickable overlays on video, you'll need a different approach (buttons outside the video cell, or the full unofficial fork with its floating content window).

## Tested with

- Avalonia 11.2.2
- LibVLCSharp 3.9.4
- VLC 3.0.22 (system-installed)
- Linux (Debian 13, Wayland + XWayland, GNOME Kiosk)
- 4K display (3840x2160 portrait, scale 1.0)
- 17 concurrent RTSP camera streams
- Intel NUC (i915 GPU, software decode via `--avcodec-hw=none`)

## Credits

Approach based on [LibVLCSharp.Avalonia.Unofficial](https://github.com/jpmikkers/LibVLCSharp.Avalonia.Unofficial) by jpmikkers, which first demonstrated the `NativeControlHost` approach for Avalonia 11. This version removes the `System.Reactive` dependency and the floating content overlay, keeping just the embedded rendering.

## Built by WINK

This fix was developed for [WINK Streaming](https://www.wink.co)'s camera monitoring platform — a 4K video wall running 17+ concurrent RTSP feeds on Avalonia with LibVLCSharp.

WINK offers enterprise video solutions and custom engineering for video walls, kiosk interfaces, and multi-camera monitoring systems. Get in touch at [wink.co](https://www.wink.co).

## License

MIT

// EmbeddedVideoView — NativeControlHost-based VideoView for LibVLCSharp + Avalonia on Linux
//
// Replaces the official LibVLCSharp.Avalonia VideoView which creates separate floating
// X11 windows that can pop out of grid cells, especially on 4K displays.
//
// This control embeds VLC's native rendering window as a child of the Avalonia window
// using NativeControlHost, so it physically cannot escape its parent bounds.
//
// Based on the approach from jpmikkers/LibVLCSharp.Avalonia.Unofficial,
// simplified to remove the System.Reactive dependency.
//
// Usage in AXAML:
//   <local:EmbeddedVideoView x:Name="VideoView"
//                             HorizontalAlignment="Stretch"
//                             VerticalAlignment="Stretch" />
//
// Usage in code-behind:
//   VideoView.MediaPlayer = myMediaPlayer;
//
// Stretch-to-fill: set AspectRatio in the OnPlaying callback ONLY (not before):
//   _mediaPlayer.Playing += (s, e) => {
//       Dispatcher.UIThread.InvokeAsync(() => {
//           _mediaPlayer.AspectRatio = $"{(int)VideoView.Bounds.Width}:{(int)VideoView.Bounds.Height}";
//       });
//   };
//
// IMPORTANT: Do NOT pre-set AspectRatio before playback starts — it causes VLC to
// fight the embedded window and can create a secondary output window.
//
// License: MIT

using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Platform;
using LibVLCSharp.Shared;

namespace Avalonia.LibVLCSharp;

/// <summary>
/// Avalonia VideoView using NativeControlHost for proper window embedding on Linux.
/// VLC's X11 window becomes a child of the Avalonia window — cannot pop out.
/// Works on Windows (HWND), Linux (XWindow), and macOS (NSObject).
/// </summary>
public class EmbeddedVideoView : NativeControlHost
{
    public static readonly DirectProperty<EmbeddedVideoView, MediaPlayer?> MediaPlayerProperty =
        AvaloniaProperty.RegisterDirect<EmbeddedVideoView, MediaPlayer?>(
            nameof(MediaPlayer),
            o => o.MediaPlayer,
            (o, v) => o.MediaPlayer = v,
            defaultBindingMode: BindingMode.TwoWay);

    private MediaPlayer? _mediaPlayer;
    private IPlatformHandle? _platformHandle;

    /// <summary>
    /// Gets or sets the LibVLC MediaPlayer. Setting this attaches the player
    /// to the native control handle for embedded rendering.
    /// </summary>
    public MediaPlayer? MediaPlayer
    {
        get => _mediaPlayer;
        set
        {
            var old = _mediaPlayer;
            if (SetAndRaise(MediaPlayerProperty, ref _mediaPlayer, value))
            {
                if (old != null)
                    DetachPlayer(old);
                if (value != null && _platformHandle != null)
                    AttachPlayer(value, _platformHandle);
            }
        }
    }

    /// <inheritdoc />
    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _platformHandle = base.CreateNativeControlCore(parent);
        if (_mediaPlayer != null && _platformHandle != null)
            AttachPlayer(_mediaPlayer, _platformHandle);
        return _platformHandle;
    }

    /// <inheritdoc />
    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        if (_mediaPlayer != null)
            DetachPlayer(_mediaPlayer);
        _platformHandle = null;
        base.DestroyNativeControlCore(control);
    }

    private static void AttachPlayer(MediaPlayer player, IPlatformHandle handle)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            player.XWindow = (uint)handle.Handle;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            player.Hwnd = handle.Handle;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            player.NsObject = handle.Handle;
    }

    private static void DetachPlayer(MediaPlayer player)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            player.XWindow = 0;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            player.Hwnd = IntPtr.Zero;
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            player.NsObject = IntPtr.Zero;
    }
}

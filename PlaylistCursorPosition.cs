using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Playlist
{
    /// <summary>
    /// Resolves the physical cursor during drag-and-drop. WPF's <see cref="System.Windows.Input.Mouse.GetPosition"/>
    /// is unreliable while another element owns capture during <c>DoDragDrop</c>.
    /// </summary>
    internal static class PlaylistCursorPosition
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out NativePoint point);

        internal static Point GetPositionRelativeTo(UIElement relativeTo)
        {
            if (relativeTo == null)
            {
                return new Point();
            }

            if (!GetCursorPos(out NativePoint native))
            {
                return System.Windows.Input.Mouse.GetPosition(relativeTo);
            }

            Point screenPoint = new Point(native.X, native.Y);
            PresentationSource source = PresentationSource.FromVisual(relativeTo);
            if (source?.CompositionTarget != null)
            {
                screenPoint = source.CompositionTarget.TransformFromDevice.Transform(screenPoint);
            }

            return relativeTo.PointFromScreen(screenPoint);
        }
    }
}

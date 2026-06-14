using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Playlist
{
    /// <summary>
    /// Shared visual-tree traversal helpers for playlist WPF chrome.
    /// </summary>
    internal static class PlaylistVisualTree
    {
        internal static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
            where T : DependencyObject
        {
            if (parent == null)
            {
                yield break;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < childCount; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (T nestedChild in FindVisualChildren<T>(child))
                {
                    yield return nestedChild;
                }
            }
        }

        internal static T FindFirstVisualChild<T>(DependencyObject parent)
            where T : DependencyObject
        {
            foreach (T child in FindVisualChildren<T>(parent))
            {
                return child;
            }

            return null;
        }

        internal static T FindFirstVisualChild<T>(DependencyObject parent, Func<T, bool> predicate)
            where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed && predicate(typed))
                {
                    return typed;
                }

                T nested = FindFirstVisualChild(child, predicate);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}

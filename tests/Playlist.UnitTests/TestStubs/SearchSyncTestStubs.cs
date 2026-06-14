using Playnite.SDK.Models;
using Playlist;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Playlist.UnitTests.TestStubs;

internal sealed class FakeMainFilterPanelBridge : IMainFilterPanelBridge
{
    public FilterPresetSettings Current { get; set; } = new FilterPresetSettings();

    public FilterPresetSettings LastApplied { get; private set; }

    public int ApplyCount { get; private set; }

    private event EventHandler<FilterPresetSettings> Changed;

    public FilterPresetSettings GetCurrentSettings() => Current;

    public void ApplySettings(FilterPresetSettings settings)
    {
        LastApplied = settings;
        ApplyCount++;
    }

    public IDisposable Subscribe(EventHandler<FilterPresetSettings> handler)
    {
        Changed += handler;
        return new Subscription(() => Changed -= handler);
    }

    public void RaiseChanged(FilterPresetSettings settings) => Changed?.Invoke(this, settings);

    private sealed class Subscription : IDisposable
    {
        private readonly Action onDispose;

        public Subscription(Action onDispose)
        {
            this.onDispose = onDispose;
        }

        public void Dispose() => onDispose();
    }
}

internal sealed class PlaylistSearchSyncTarget : IPlaylistSearchSyncTarget
{
    private string searchQuery = string.Empty;

    public string SearchQuery
    {
        get => searchQuery;
        set
        {
            if (searchQuery == value)
            {
                return;
            }

            searchQuery = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

using Playnite.SDK;
using Playnite.SDK.Models;
using System.Collections.Generic;

namespace Playlist
{
    public sealed class CompletionStatusMenuEntry
    {
        public CompletionStatusMenuEntry(
            CompletionStatus status,
            bool isSyncableTier,
            RelayCommand<IEnumerable<object>> command)
        {
            Status = status;
            IsSyncableTier = isSyncableTier;
            Command = command;
        }

        public CompletionStatus Status { get; }

        public bool IsSyncableTier { get; }

        public RelayCommand<IEnumerable<object>> Command { get; }
    }
}

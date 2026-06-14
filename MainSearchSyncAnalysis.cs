using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Playlist
{
    /// <summary>How a scoped filter field should be updated when pushing Playlist syntax to the main panel.</summary>
    internal enum ScopePushMode
    {
        /// <summary>Leave the main-panel field unchanged.</summary>
        Skip,
        /// <summary>Clear the main-panel field.</summary>
        Clear,
        /// <summary>Clear a negated scoped field (playlist-only syntax).</summary>
        ClearNegated,
        /// <summary>Replace with a single resolved value.</summary>
        PushSingle,
        /// <summary>Replace with an OR list of values.</summary>
        PushOrList,
        /// <summary>Replace with an AND list of values.</summary>
        PushAndList,
    }

    /// <summary>Per-scope push instruction produced by <see cref="MainSearchFilterMapper.AnalyzePlaylistQuery"/>.</summary>
    internal sealed class ScopePushPlan
    {
        public static ScopePushPlan Skip { get; } = new ScopePushPlan(ScopePushMode.Skip, Array.Empty<string>());
        public static ScopePushPlan ClearField { get; } = new ScopePushPlan(ScopePushMode.Clear, Array.Empty<string>());
        public static ScopePushPlan ClearNegatedField { get; } = new ScopePushPlan(ScopePushMode.ClearNegated, Array.Empty<string>());

        public ScopePushPlan(ScopePushMode mode, IReadOnlyList<string> values)
        {
            Mode = mode;
            Values = values ?? Array.Empty<string>();
        }

        public ScopePushMode Mode { get; }
        public IReadOnlyList<string> Values { get; }
    }

    /// <summary>Parsed push plan for one Playlist query, including syncability flags.</summary>
    internal sealed class MainSearchSyncAnalysis
    {
        public MainSearchSyncAnalysis(
            string name,
            ScopePushPlan developer,
            ScopePushPlan tag,
            ScopePushPlan genre,
            ScopePushPlan publisher,
            ScopePushPlan category,
            ScopePushPlan feature,
            bool hasPlaylistOnlySyntax,
            bool hasMatchAllConflict,
            bool? useAndFilteringStyle)
        {
            Name = name ?? string.Empty;
            Developer = developer ?? ScopePushPlan.Skip;
            Tag = tag ?? ScopePushPlan.Skip;
            Genre = genre ?? ScopePushPlan.Skip;
            Publisher = publisher ?? ScopePushPlan.Skip;
            Category = category ?? ScopePushPlan.Skip;
            Feature = feature ?? ScopePushPlan.Skip;
            HasPlaylistOnlySyntax = hasPlaylistOnlySyntax;
            HasMatchAllConflict = hasMatchAllConflict;
            UseAndFilteringStyle = useAndFilteringStyle;
        }

        public string Name { get; }
        public ScopePushPlan Developer { get; }
        public ScopePushPlan Tag { get; }
        public ScopePushPlan Genre { get; }
        public ScopePushPlan Publisher { get; }
        public ScopePushPlan Category { get; }
        public ScopePushPlan Feature { get; }
        public bool HasPlaylistOnlySyntax { get; }
        public bool? UseAndFilteringStyle { get; }
        public bool HasMatchAllConflict { get; }

        /// <summary>True when every clause in the query can be represented on the main filter panel.</summary>
        public bool IsFullySyncable => !HasPlaylistOnlySyntax;
    }
}

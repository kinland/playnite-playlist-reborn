using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Playlist
{
    internal enum ScopePushMode
    {
        Skip,
        Clear,
        ClearNegated,
        PushSingle,
        PushOrList,
        PushAndList,
    }

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
            UseAndFilteringStyle = useAndFilteringStyle;
            HasMatchAllConflict = ComputeMatchAllConflict();
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

        public bool IsFullySyncable => !HasPlaylistOnlySyntax;

        private bool ComputeMatchAllConflict()
        {
            bool needsAnd = HasMode(ScopePushMode.PushAndList);
            bool needsOr = HasMode(ScopePushMode.PushOrList) || HasMode(ScopePushMode.PushSingle);
            return needsAnd && needsOr;
        }

        private bool HasMode(ScopePushMode mode)
        {
            return Developer.Mode == mode
                || Tag.Mode == mode
                || Genre.Mode == mode
                || Publisher.Mode == mode
                || Category.Mode == mode
                || Feature.Mode == mode;
        }

        public ScopePushPlan GetPlan(ScopedFilterKind kind)
        {
            switch (kind)
            {
                case ScopedFilterKind.Developer: return Developer;
                case ScopedFilterKind.Tag: return Tag;
                case ScopedFilterKind.Genre: return Genre;
                case ScopedFilterKind.Publisher: return Publisher;
                case ScopedFilterKind.Category: return Category;
                case ScopedFilterKind.Feature: return Feature;
                default: return ScopePushPlan.Skip;
            }
        }
    }
}

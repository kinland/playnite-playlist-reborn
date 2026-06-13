using System.Collections.Generic;

namespace Playlist
{
    internal enum ScopedValueCombine
    {
        Or,
        And,
    }

    internal sealed class ScopedSearchClause
    {
        public ScopedFilterKind Kind { get; }
        public bool Negated { get; }
        public ScopedValueCombine CombineWithin { get; }
        public IReadOnlyList<string> Values { get; }

        public ScopedSearchClause(
            ScopedFilterKind kind,
            bool negated,
            ScopedValueCombine combineWithin,
            IReadOnlyList<string> values)
        {
            Kind = kind;
            Negated = negated;
            CombineWithin = combineWithin;
            Values = values ?? new string[0];
        }
    }
}

using System.Collections.Generic;

namespace Playlist
{
    /// <summary>
    /// Pure overlap suppression for HLTB progress bar interior time labels.
    /// </summary>
    internal static class HltbInteriorLabelOverlap
    {
        internal readonly struct LabelPlan
        {
            public LabelPlan(double centerX, double sliceWidth)
            {
                CenterX = centerX;
                SliceWidth = sliceWidth;
            }

            public double CenterX { get; }

            public double SliceWidth { get; }
        }

        internal static void SuppressOverlapping(
            IReadOnlyList<LabelPlan> plans,
            IReadOnlyList<double> labelWidths,
            bool[] show,
            double padding = 2)
        {
            for (int i = 0; i < plans.Count; i++)
            {
                if (!show[i])
                {
                    continue;
                }

                for (int j = i + 1; j < plans.Count; j++)
                {
                    if (!show[j])
                    {
                        continue;
                    }

                    double centerGap = plans[j].CenterX - plans[i].CenterX;
                    double requiredGap = (labelWidths[i] + labelWidths[j]) / 2.0 + padding;
                    if (centerGap >= requiredGap)
                    {
                        continue;
                    }

                    if (plans[i].SliceWidth > plans[j].SliceWidth)
                    {
                        show[j] = false;
                    }
                    else if (plans[j].SliceWidth > plans[i].SliceWidth)
                    {
                        show[i] = false;
                        break;
                    }
                    else
                    {
                        show[j] = false;
                    }
                }
            }
        }
    }
}

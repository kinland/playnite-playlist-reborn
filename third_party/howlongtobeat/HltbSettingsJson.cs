using System;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace Playlist
{
    /// <summary>
    /// Merges HowLongToBeat plugin settings from Playnite JSON (nested keys, camelCase)
    /// into <see cref="HltbRenderSettings"/>. DataContractJsonSerializer alone often misses Playnite's on-disk shape.
    /// </summary>
    internal static class HltbSettingsJson
    {
        public static void MergeInto(string json, HltbRenderSettings target)
        {
            if (string.IsNullOrWhiteSpace(json) || target == null)
            {
                return;
            }

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    Walk(doc.RootElement, target);
                }
            }
            catch
            {
            }
        }

        private static void Walk(JsonElement el, HltbRenderSettings target)
        {
            if (el.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (JsonProperty prop in el.EnumerateObject())
            {
                TryApplyBrush(prop.Name, prop.Value, target);
                TryApplyBool(prop.Name, prop.Value, target);

                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    Walk(prop.Value, target);
                }
                else if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in prop.Value.EnumerateArray())
                    {
                        Walk(item, target);
                    }
                }
            }
        }

        private static void TryApplyBool(string name, JsonElement value, HltbRenderSettings target)
        {
            if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
            {
                return;
            }

            bool b = value.GetBoolean();
            switch (Canonical(name))
            {
                case "integrationviewitemonlyhour":
                    target.IntegrationViewItemOnlyHour = b;
                    break;
                case "usehtltbclassic":
                    target.UseClassic = b;
                    break;
                case "usehtltbaverage":
                    target.UseAverage = b;
                    break;
                case "usehtltbmedian":
                    target.UseMedian = b;
                    break;
                case "usehtltbrushed":
                    target.UseRushed = b;
                    break;
                case "usehtltbleisure":
                    target.UseLeisure = b;
                    break;
                case "enableintegrationviewitem":
                    target.EnableIntegrationViewItem = b;
                    break;
                case "enableintegrationbutton":
                    target.EnableIntegrationButton = b;
                    break;
                case "enableintegrationprogressbar":
                    target.EnableIntegrationProgressBar = b;
                    break;
                case "showmaintime":
                    target.ShowMainTime = b;
                    break;
                case "showextratime":
                    target.ShowExtraTime = b;
                    break;
                case "showcompletionisttime":
                    target.ShowCompletionistTime = b;
                    break;
                case "showsolotime":
                    target.ShowSoloTime = b;
                    break;
                case "showcooptime":
                    target.ShowCoOpTime = b;
                    break;
                case "showvstime":
                    target.ShowVsTime = b;
                    break;
                case "progressbarshowtime":
                    target.ProgressBarShowTime = b;
                    break;
                case "progressbarshowtimeinterior":
                    target.ProgressBarShowTimeInterior = b;
                    break;
                case "progressbarshowtimeabove":
                    target.ProgressBarShowTimeAbove = b;
                    break;
                case "progressbarshowtimebelow":
                    target.ProgressBarShowTimeBelow = b;
                    break;
                case "progressbarshowtooltip":
                    target.ProgressBarShowToolTip = b;
                    break;
            }
        }

        private static void TryApplyBrush(string name, JsonElement value, HltbRenderSettings target)
        {
            Brush brush = TryParseBrush(value);
            if (brush == null)
            {
                return;
            }

            Color? c = TryGetRepresentativeColor(brush);

            switch (Canonical(name))
            {
                case "firstcolorbrush":
                    target.FirstBrush = brush;
                    if (c.HasValue) target.FirstColor = c.Value;
                    break;
                case "secondcolorbrush":
                    target.SecondBrush = brush;
                    if (c.HasValue) target.SecondColor = c.Value;
                    break;
                case "thirdcolorbrush":
                    target.ThirdBrush = brush;
                    if (c.HasValue) target.ThirdColor = c.Value;
                    break;
                case "firstmulticolorbrush":
                    target.FirstMultiBrush = brush;
                    if (c.HasValue) target.FirstMultiColor = c.Value;
                    break;
                case "secondmulticolorbrush":
                    target.SecondMultiBrush = brush;
                    if (c.HasValue) target.SecondMultiColor = c.Value;
                    break;
                case "thirdmulticolorbrush":
                    target.ThirdMultiBrush = brush;
                    if (c.HasValue) target.ThirdMultiColor = c.Value;
                    break;
                case "thumbsolidcolorbrush":
                    target.ThumbPlaytimeBrush = brush;
                    if (c.HasValue) target.ThumbPlaytimeColor = c.Value;
                    break;
                case "thumblineargradient":
                    target.ThumbPlaytimeBrush = brush;
                    if (c.HasValue) target.ThumbPlaytimeColor = c.Value;
                    break;
                case "firstlineargradient":
                    target.FirstBrush = brush;
                    if (c.HasValue) target.FirstColor = c.Value;
                    break;
                case "secondlineargradient":
                    target.SecondBrush = brush;
                    if (c.HasValue) target.SecondColor = c.Value;
                    break;
                case "thirdlineargradient":
                    target.ThirdBrush = brush;
                    if (c.HasValue) target.ThirdColor = c.Value;
                    break;
                case "firstmultilineargradient":
                    target.FirstMultiBrush = brush;
                    if (c.HasValue) target.FirstMultiColor = c.Value;
                    break;
                case "secondmultilineargradient":
                    target.SecondMultiBrush = brush;
                    if (c.HasValue) target.SecondMultiColor = c.Value;
                    break;
                case "thirdmultilineargradient":
                    target.ThirdMultiBrush = brush;
                    if (c.HasValue) target.ThirdMultiColor = c.Value;
                    break;
            }
        }

        private static Brush TryParseBrush(JsonElement brushEl)
        {
            if (brushEl.ValueKind == JsonValueKind.String)
            {
                string s = brushEl.GetString();
                Color? c = TryParseColorString(s);
                if (c.HasValue)
                {
                    return new SolidColorBrush(c.Value);
                }
            }

            if (brushEl.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (TryGetPropertyIgnoreCase(brushEl, "GradientStops", out JsonElement stopsEl) &&
                stopsEl.ValueKind == JsonValueKind.Array)
            {
                var brush = new LinearGradientBrush();
                if (TryGetPropertyIgnoreCase(brushEl, "StartPoint", out JsonElement sp) && TryParsePoint(sp, out Point start))
                {
                    brush.StartPoint = start;
                }
                else
                {
                    brush.StartPoint = new Point(0, 0);
                }

                if (TryGetPropertyIgnoreCase(brushEl, "EndPoint", out JsonElement ep) && TryParsePoint(ep, out Point end))
                {
                    brush.EndPoint = end;
                }
                else
                {
                    brush.EndPoint = new Point(1, 1);
                }

                foreach (JsonElement stop in stopsEl.EnumerateArray())
                {
                    if (!TryGetPropertyIgnoreCase(stop, "Color", out JsonElement stopColorEl))
                    {
                        continue;
                    }

                    Color? stopColor = TryParseColorElement(stopColorEl);
                    if (!stopColor.HasValue)
                    {
                        continue;
                    }

                    double offset = 0;
                    if (TryGetPropertyIgnoreCase(stop, "Offset", out JsonElement offsetEl))
                    {
                        offset = ReadDouble(offsetEl, 0);
                    }

                    brush.GradientStops.Add(new GradientStop(stopColor.Value, offset));
                }

                if (brush.GradientStops.Count > 0)
                {
                    return brush;
                }
            }

            Color? solid = TryParseColorElement(brushEl);
            if (solid.HasValue)
            {
                return new SolidColorBrush(solid.Value);
            }

            return null;
        }

        private static Color? TryGetRepresentativeColor(Brush brush)
        {
            if (brush is SolidColorBrush sb)
            {
                return sb.Color;
            }

            if (brush is LinearGradientBrush lb && lb.GradientStops.Count > 0)
            {
                return lb.GradientStops[0].Color;
            }

            return null;
        }

        private static Color? TryParseColorElement(JsonElement colorEl)
        {
            if (colorEl.ValueKind == JsonValueKind.String)
            {
                return TryParseColorString(colorEl.GetString());
            }

            if (colorEl.ValueKind == JsonValueKind.Object)
            {
                if (TryGetPropertyIgnoreCase(colorEl, "Color", out JsonElement nestedColor))
                {
                    Color? nested = TryParseColorElement(nestedColor);
                    if (nested.HasValue)
                    {
                        return nested;
                    }
                }

                byte? a = ReadByteMember(colorEl, "A");
                byte? r = ReadByteMember(colorEl, "R");
                byte? g = ReadByteMember(colorEl, "G");
                byte? b = ReadByteMember(colorEl, "B");
                if (a.HasValue && r.HasValue && g.HasValue && b.HasValue)
                {
                    return Color.FromArgb(a.Value, r.Value, g.Value, b.Value);
                }
            }

            return null;
        }

        private static Color? TryParseColorString(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            try
            {
                return (Color)ColorConverter.ConvertFromString(s);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryParsePoint(JsonElement el, out Point p)
        {
            p = new Point(0, 0);
            if (el.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!TryGetPropertyIgnoreCase(el, "X", out JsonElement xEl) ||
                !TryGetPropertyIgnoreCase(el, "Y", out JsonElement yEl))
            {
                return false;
            }

            p = new Point(ReadDouble(xEl, 0), ReadDouble(yEl, 0));
            return true;
        }

        private static double ReadDouble(JsonElement el, double fallback)
        {
            if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out double n))
            {
                return n;
            }

            if (el.ValueKind == JsonValueKind.String &&
                double.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out n))
            {
                return n;
            }

            return fallback;
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement obj, string propName, out JsonElement value)
        {
            foreach (JsonProperty p in obj.EnumerateObject())
            {
                if (p.Name.Equals(propName, StringComparison.OrdinalIgnoreCase))
                {
                    value = p.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static byte? ReadByteMember(JsonElement obj, string prop)
        {
            foreach (JsonProperty p in obj.EnumerateObject())
            {
                if (!p.Name.Equals(prop, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out int n))
                {
                    return (byte)Math.Min(255, Math.Max(0, n));
                }

                if (p.Value.ValueKind == JsonValueKind.String && byte.TryParse(p.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b))
                {
                    return b;
                }
            }

            return null;
        }

        private static string Canonical(string name)
        {
            return string.IsNullOrEmpty(name) ? string.Empty : name.Trim().ToLowerInvariant();
        }
    }
}

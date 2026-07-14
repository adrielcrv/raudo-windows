using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;

namespace Raudo
{
    internal enum RaudoActionGlyph
    {
        Pulse,
        Capture,
        MainWindow,
        Mini,
        DesktopLeft,
        DesktopRight,
        Window,
        Application
    }

    internal enum RaudoActionKind
    {
        Raudo,
        Window,
        Application
    }

    internal sealed class RaudoAction
    {
        private readonly Func<string> executor;
        private readonly string normalizedTitle;
        private readonly string normalizedDescription;
        private readonly string normalizedKeywords;
        private readonly string normalizedCombined;

        public RaudoAction(
            string id,
            string title,
            string description,
            string keywords,
            string shortcutHint,
            RaudoActionGlyph glyph,
            Action execute)
            : this(
                id,
                title,
                description,
                keywords,
                shortcutHint,
                glyph,
                RaudoActionKind.Raudo,
                true,
                2,
                Wrap(execute))
        {
        }

        public RaudoAction(
            string id,
            string title,
            string description,
            string keywords,
            string shortcutHint,
            RaudoActionGlyph glyph,
            RaudoActionKind kind,
            bool showWhenQueryEmpty,
            int searchPriority,
            Func<string> execute)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("La acción requiere un identificador.", "id");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("La acción requiere un título.", "title");
            }

            if (execute == null)
            {
                throw new ArgumentNullException("execute");
            }

            Id = id;
            Title = title;
            Description = description ?? string.Empty;
            Keywords = keywords ?? string.Empty;
            ShortcutHint = shortcutHint ?? string.Empty;
            Glyph = glyph;
            Kind = kind;
            ShowWhenQueryEmpty = showWhenQueryEmpty;
            SearchPriority = Math.Max(0, searchPriority);
            executor = execute;
            normalizedTitle = RaudoActionCatalog.Normalize(Title);
            normalizedDescription = RaudoActionCatalog.Normalize(Description);
            normalizedKeywords = RaudoActionCatalog.Normalize(Keywords);
            normalizedCombined = normalizedTitle
                + " "
                + normalizedDescription
                + " "
                + normalizedKeywords;
        }

        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public string Keywords { get; private set; }
        public string ShortcutHint { get; private set; }
        public RaudoActionGlyph Glyph { get; private set; }
        public RaudoActionKind Kind { get; private set; }
        public bool ShowWhenQueryEmpty { get; private set; }
        public int SearchPriority { get; private set; }

        public string Execute()
        {
            try
            {
                return executor();
            }
            catch (Exception)
            {
                return "No se pudo completar la acción.";
            }
        }

        public int GetMatchScore(string normalizedQuery)
        {
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return 0;
            }

            string[] tokens = normalizedQuery.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                if (normalizedCombined.IndexOf(token, StringComparison.Ordinal) < 0)
                {
                    return int.MaxValue;
                }
            }

            if (string.Equals(normalizedTitle, normalizedQuery, StringComparison.Ordinal))
            {
                return 0;
            }

            if (normalizedTitle.StartsWith(normalizedQuery, StringComparison.Ordinal))
            {
                return 10;
            }

            if (normalizedTitle.IndexOf(normalizedQuery, StringComparison.Ordinal) >= 0)
            {
                return 20;
            }

            if (normalizedKeywords.IndexOf(normalizedQuery, StringComparison.Ordinal) >= 0)
            {
                return 30;
            }

            return 40;
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Description)
                ? Title
                : Title + ", " + Description;
        }

        private static Func<string> Wrap(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException("execute");
            }

            return delegate
            {
                action();
                return null;
            };
        }
    }

    internal sealed class RaudoActionCatalog
    {
        private readonly Func<IList<RaudoAction>> source;
        private readonly List<RaudoAction> snapshot;

        public RaudoActionCatalog(Func<IList<RaudoAction>> actionSource)
        {
            if (actionSource == null)
            {
                throw new ArgumentNullException("actionSource");
            }

            source = actionSource;
            snapshot = new List<RaudoAction>();
        }

        public int Count
        {
            get { return snapshot.Count; }
        }

        public void Refresh()
        {
            IList<RaudoAction> actions = source() ?? new List<RaudoAction>();
            snapshot.Clear();
            foreach (RaudoAction action in actions)
            {
                if (action != null)
                {
                    snapshot.Add(action);
                }
            }
        }

        public IList<RaudoAction> Search(string query)
        {
            string normalizedQuery = Normalize(query);
            List<ScoredAction> matches = new List<ScoredAction>();
            for (int index = 0; index < snapshot.Count; index++)
            {
                RaudoAction action = snapshot[index];
                if (normalizedQuery.Length == 0 && !action.ShowWhenQueryEmpty)
                {
                    continue;
                }

                int score = action.GetMatchScore(normalizedQuery);
                if (score != int.MaxValue)
                {
                    matches.Add(new ScoredAction(
                        action,
                        score + action.SearchPriority,
                        index));
                }
            }

            matches.Sort(delegate(ScoredAction left, ScoredAction right)
            {
                int scoreComparison = left.Score.CompareTo(right.Score);
                return scoreComparison != 0
                    ? scoreComparison
                    : left.Order.CompareTo(right.Order);
            });

            int resultCount = normalizedQuery.Length == 0
                ? matches.Count
                : Math.Min(12, matches.Count);
            List<RaudoAction> result = new List<RaudoAction>(resultCount);
            foreach (ScoredAction match in matches)
            {
                if (result.Count >= resultCount)
                {
                    break;
                }

                result.Add(match.Action);
            }

            return result;
        }

        internal static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string decomposed = value.Trim().ToLowerInvariant().Normalize(
                NormalizationForm.FormD);
            StringBuilder result = new StringBuilder(decomposed.Length);
            bool previousWasSpace = false;
            foreach (char character in decomposed)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                bool isSpace = char.IsWhiteSpace(character);
                if (isSpace)
                {
                    if (!previousWasSpace)
                    {
                        result.Append(' ');
                    }
                }
                else
                {
                    result.Append(character);
                }

                previousWasSpace = isSpace;
            }

            return result.ToString().Trim();
        }

        private sealed class ScoredAction
        {
            public ScoredAction(RaudoAction action, int score, int order)
            {
                Action = action;
                Score = score;
                Order = order;
            }

            public RaudoAction Action { get; private set; }
            public int Score { get; private set; }
            public int Order { get; private set; }
        }
    }

    internal static class RaudoActionGlyphDrawing
    {
        public static void Draw(
            Graphics graphics,
            Rectangle bounds,
            RaudoActionGlyph glyph,
            Color color)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float scale = Math.Min(bounds.Width, bounds.Height) / 24F;
                float left = bounds.Left + ((bounds.Width - (24F * scale)) / 2F);
                float top = bounds.Top + ((bounds.Height - (24F * scale)) / 2F);
                using (Pen pen = new Pen(color, Math.Max(1.4F, 1.7F * scale)))
                using (SolidBrush brush = new SolidBrush(color))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    switch (glyph)
                    {
                        case RaudoActionGlyph.Capture:
                            DrawCapture(graphics, pen, left, top, scale);
                            break;
                        case RaudoActionGlyph.MainWindow:
                            DrawWindow(graphics, pen, left, top, scale);
                            break;
                        case RaudoActionGlyph.Mini:
                            DrawMini(graphics, pen, brush, left, top, scale);
                            break;
                        case RaudoActionGlyph.DesktopLeft:
                            DrawDesktop(graphics, pen, left, top, scale, false);
                            break;
                        case RaudoActionGlyph.DesktopRight:
                            DrawDesktop(graphics, pen, left, top, scale, true);
                            break;
                        case RaudoActionGlyph.Window:
                            DrawOpenWindow(graphics, pen, left, top, scale);
                            break;
                        case RaudoActionGlyph.Application:
                            DrawApplication(graphics, pen, left, top, scale);
                            break;
                        default:
                            DrawPulse(graphics, pen, left, top, scale);
                            break;
                    }
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private static void DrawPulse(Graphics graphics, Pen pen, float left, float top, float scale)
        {
            PointF[] points =
            {
                new PointF(left + (2F * scale), top + (13F * scale)),
                new PointF(left + (7F * scale), top + (13F * scale)),
                new PointF(left + (10F * scale), top + (6F * scale)),
                new PointF(left + (14F * scale), top + (18F * scale)),
                new PointF(left + (17F * scale), top + (11F * scale)),
                new PointF(left + (22F * scale), top + (11F * scale))
            };
            graphics.DrawLines(pen, points);
        }

        private static void DrawCapture(Graphics graphics, Pen pen, float left, float top, float scale)
        {
            float shortSide = 5F * scale;
            float start = 4F * scale;
            float end = 20F * scale;
            graphics.DrawLine(pen, left + start, top + (start + shortSide), left + start, top + start);
            graphics.DrawLine(pen, left + start, top + start, left + (start + shortSide), top + start);
            graphics.DrawLine(pen, left + (end - shortSide), top + start, left + end, top + start);
            graphics.DrawLine(pen, left + end, top + start, left + end, top + (start + shortSide));
            graphics.DrawLine(pen, left + end, top + (end - shortSide), left + end, top + end);
            graphics.DrawLine(pen, left + end, top + end, left + (end - shortSide), top + end);
            graphics.DrawLine(pen, left + (start + shortSide), top + end, left + start, top + end);
            graphics.DrawLine(pen, left + start, top + end, left + start, top + (end - shortSide));
        }

        private static void DrawWindow(Graphics graphics, Pen pen, float left, float top, float scale)
        {
            RectangleF window = new RectangleF(
                left + (3F * scale),
                top + (4F * scale),
                18F * scale,
                16F * scale);
            graphics.DrawRectangle(
                pen,
                window.X,
                window.Y,
                window.Width,
                window.Height);
            graphics.DrawLine(
                pen,
                window.Left,
                window.Top + (5F * scale),
                window.Right,
                window.Top + (5F * scale));
        }

        private static void DrawMini(
            Graphics graphics,
            Pen pen,
            Brush brush,
            float left,
            float top,
            float scale)
        {
            RectangleF body = new RectangleF(
                left + (4F * scale),
                top + (7F * scale),
                16F * scale,
                10F * scale);
            graphics.DrawRectangle(pen, body.X, body.Y, body.Width, body.Height);
            graphics.FillEllipse(
                brush,
                left + (10F * scale),
                top + (10F * scale),
                4F * scale,
                4F * scale);
        }

        private static void DrawDesktop(
            Graphics graphics,
            Pen pen,
            float left,
            float top,
            float scale,
            bool right)
        {
            RectangleF desktop = new RectangleF(
                left + (3F * scale),
                top + (5F * scale),
                18F * scale,
                13F * scale);
            graphics.DrawRectangle(
                pen,
                desktop.X,
                desktop.Y,
                desktop.Width,
                desktop.Height);
            float centerX = left + (12F * scale);
            float centerY = top + (11.5F * scale);
            float direction = right ? 1F : -1F;
            graphics.DrawLine(
                pen,
                centerX - (4F * direction * scale),
                centerY,
                centerX + (4F * direction * scale),
                centerY);
            graphics.DrawLine(
                pen,
                centerX + (4F * direction * scale),
                centerY,
                centerX + (1F * direction * scale),
                centerY - (3F * scale));
            graphics.DrawLine(
                pen,
                centerX + (4F * direction * scale),
                centerY,
                centerX + (1F * direction * scale),
                centerY + (3F * scale));
        }

        private static void DrawOpenWindow(
            Graphics graphics,
            Pen pen,
            float left,
            float top,
            float scale)
        {
            RectangleF back = new RectangleF(
                left + (6F * scale),
                top + (4F * scale),
                15F * scale,
                13F * scale);
            RectangleF front = new RectangleF(
                left + (3F * scale),
                top + (7F * scale),
                15F * scale,
                13F * scale);
            graphics.DrawRectangle(pen, back.X, back.Y, back.Width, back.Height);
            graphics.DrawRectangle(pen, front.X, front.Y, front.Width, front.Height);
            graphics.DrawLine(
                pen,
                front.Left,
                front.Top + (4F * scale),
                front.Right,
                front.Top + (4F * scale));
        }

        private static void DrawApplication(
            Graphics graphics,
            Pen pen,
            float left,
            float top,
            float scale)
        {
            float tile = 7F * scale;
            float gap = 3F * scale;
            float startX = left + (4F * scale);
            float startY = top + (4F * scale);
            graphics.DrawRectangle(pen, startX, startY, tile, tile);
            graphics.DrawRectangle(pen, startX + tile + gap, startY, tile, tile);
            graphics.DrawRectangle(pen, startX, startY + tile + gap, tile, tile);
            graphics.DrawRectangle(
                pen,
                startX + tile + gap,
                startY + tile + gap,
                tile,
                tile);
        }
    }
}

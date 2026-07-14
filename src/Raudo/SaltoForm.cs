using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Raudo
{
    internal sealed class SaltoForm : Form
    {
        private const int OpeningDurationMilliseconds = 140;
        private readonly RaudoActionCatalog catalog;
        private readonly RoundedPanel searchSurface;
        private readonly SaltoSearchGlyph searchGlyph;
        private readonly TextBox searchBox;
        private readonly Label escapeLabel;
        private readonly Label sectionLabel;
        private readonly SaltoResultList resultList;
        private readonly Label emptyLabel;
        private readonly Panel footerDivider;
        private readonly Label localLabel;
        private readonly Label keyboardHintLabel;
        private readonly Timer openingTimer;
        private readonly Stopwatch openingWatch;

        private ThemePalette palette;
        private bool allowClose;
        private bool applicationsLoading;
        private string applicationCatalogError;
        private string executionError;

        public SaltoForm(RaudoActionCatalog actionCatalog)
        {
            if (actionCatalog == null)
            {
                throw new ArgumentNullException("actionCatalog");
            }

            catalog = actionCatalog;
            Text = "Salto · Raudo";
            AccessibleName = "Salto de Raudo";
            AccessibleDescription =
                "Busca ventanas, aplicaciones y carpetas, o calcula localmente";
            ClientSize = new Size(640, 432);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point);
            DoubleBuffered = true;

            searchSurface = new RoundedPanel();
            searchSurface.Location = new Point(16, 16);
            searchSurface.Size = new Size(608, 56);
            searchSurface.Radius = 14;
            Controls.Add(searchSurface);

            searchGlyph = new SaltoSearchGlyph();
            searchGlyph.Location = new Point(17, 16);
            searchSurface.Controls.Add(searchGlyph);

            searchBox = new TextBox();
            searchBox.BorderStyle = BorderStyle.None;
            searchBox.Font = new Font(
                "Segoe UI",
                11F,
                FontStyle.Regular,
                GraphicsUnit.Point);
            searchBox.Location = new Point(52, 17);
            searchBox.Size = new Size(466, 24);
            searchBox.TabIndex = 0;
            searchBox.AccessibleName = "Buscar en Salto";
            searchBox.AccessibleDescription =
                "Escribe para buscar ventanas, aplicaciones y carpetas, o calcular";
            searchBox.TextChanged += SearchBoxTextChanged;
            searchBox.HandleCreated += delegate { ApplySearchCue(); };
            searchSurface.Controls.Add(searchBox);

            escapeLabel = CreateLabel(
                "Esc",
                8F,
                FontStyle.Bold,
                new Point(548, 16),
                new Size(42, 24));
            escapeLabel.TextAlign = ContentAlignment.MiddleCenter;
            searchSurface.Controls.Add(escapeLabel);

            sectionLabel = CreateLabel(
                "ACCIONES",
                7.75F,
                FontStyle.Bold,
                new Point(20, 84),
                new Size(180, 20));
            Controls.Add(sectionLabel);

            resultList = new SaltoResultList();
            resultList.Location = new Point(16, 106);
            resultList.Size = new Size(608, 272);
            resultList.TabIndex = 1;
            resultList.AccessibleName = "Resultados de Salto";
            resultList.MouseClick += ResultListMouseClick;
            resultList.KeyDown += ResultListKeyDown;
            resultList.SelectedIndexChanged += delegate { UpdateKeyboardHint(); };
            Controls.Add(resultList);

            emptyLabel = CreateLabel(
                "No hay resultados que coincidan",
                10F,
                FontStyle.Regular,
                new Point(20, 150),
                new Size(600, 36));
            emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            emptyLabel.Visible = false;
            Controls.Add(emptyLabel);

            footerDivider = new Panel();
            footerDivider.Location = new Point(16, 390);
            footerDivider.Size = new Size(608, 1);
            Controls.Add(footerDivider);

            localLabel = CreateLabel(
                "●  Raudo se ejecuta localmente",
                8F,
                FontStyle.Regular,
                new Point(21, 400),
                new Size(260, 22));
            Controls.Add(localLabel);

            keyboardHintLabel = CreateLabel(
                "↑↓  mover     Enter  abrir",
                8F,
                FontStyle.Regular,
                new Point(378, 400),
                new Size(242, 22));
            keyboardHintLabel.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(keyboardHintLabel);

            openingTimer = new Timer();
            openingTimer.Interval = 15;
            openingTimer.Tick += OpeningTimerTick;
            openingWatch = new Stopwatch();

            Deactivate += delegate { HideSalto(); };
            VisibleChanged += SaltoVisibleChanged;
            ApplyTheme(ThemeService.Current());
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            BackColor = palette.Window;
            ForeColor = palette.Text;
            searchSurface.BackColor = palette.SurfaceRaised;
            searchSurface.BorderColor = palette.IsHighContrast
                ? palette.Border
                : Color.Transparent;
            searchBox.BackColor = palette.SurfaceRaised;
            searchBox.ForeColor = palette.Text;
            escapeLabel.BackColor = palette.Surface;
            escapeLabel.ForeColor = palette.TextMuted;
            sectionLabel.ForeColor = palette.TextFaint;
            emptyLabel.ForeColor = palette.TextMuted;
            footerDivider.BackColor = palette.Border;
            localLabel.ForeColor = string.IsNullOrWhiteSpace(executionError)
                ? palette.TextMuted
                : palette.Danger;
            keyboardHintLabel.ForeColor = palette.TextFaint;
            searchGlyph.ApplyTheme(palette);
            resultList.ApplyTheme(palette);
            ApplySearchCue();
            Invalidate();

            if (IsHandleCreated)
            {
                WindowTheme.Apply(Handle, palette.IsDark);
            }
        }

        public void ToggleSalto()
        {
            if (Visible)
            {
                HideSalto();
            }
            else
            {
                ShowSalto();
            }
        }

        public void ShowSalto()
        {
            ResetFooterStatus();
            catalog.Refresh();
            searchBox.Text = string.Empty;
            RefreshResults();
            PositionOnForegroundScreen();

            bool animate = MotionSettings.ClientAreaAnimationsEnabled();
            Opacity = animate ? 0D : 1D;
            if (!Visible)
            {
                Show();
            }

            Activate();
            BringToFront();
            searchBox.Focus();
            searchBox.SelectAll();

            if (animate)
            {
                openingWatch.Restart();
                openingTimer.Start();
            }
        }

        public void HideSalto()
        {
            openingTimer.Stop();
            openingWatch.Reset();
            Opacity = 1D;
            if (Visible)
            {
                Hide();
            }
        }

        public void EnsureVisibleOnScreen()
        {
            if (Visible)
            {
                PositionOnForegroundScreen();
            }
        }

        public void AllowCloseAndClose()
        {
            allowClose = true;
            openingTimer.Stop();
            Close();
        }

        public void SetApplicationCatalogState(bool loading, string error)
        {
            applicationsLoading = loading;
            applicationCatalogError = error;
            if (Visible)
            {
                RefreshResults();
            }
        }

        public void RefreshCatalog()
        {
            catalog.Refresh();
            RefreshResults();
        }

        internal int ResultCountForTesting
        {
            get { return resultList.Items.Count; }
        }

        internal string SelectedActionIdForTesting
        {
            get
            {
                RaudoAction selected = resultList.SelectedItem as RaudoAction;
                return selected == null ? null : selected.Id;
            }
        }

        internal bool TransitionRunningForTesting
        {
            get { return openingTimer.Enabled; }
        }

        internal string KeyboardHintForTesting
        {
            get { return keyboardHintLabel.Text; }
        }

        internal void SetQueryForTesting(string query)
        {
            searchBox.Text = query ?? string.Empty;
            RefreshResults();
        }

        internal void MoveSelectionForTesting(int direction)
        {
            MoveSelection(direction);
        }

        internal void ExecuteSelectedForTesting()
        {
            ExecuteSelected();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int dropShadow = 0x00020000;
                CreateParams parameters = base.CreateParams;
                parameters.ClassStyle |= dropShadow;
                return parameters;
            }
        }

        protected override void OnHandleCreated(EventArgs eventArgs)
        {
            base.OnHandleCreated(eventArgs);
            WindowTheme.Apply(Handle, palette == null ? false : palette.IsDark);
        }

        protected override void OnResize(EventArgs eventArgs)
        {
            base.OnResize(eventArgs);
            if (Width > 1 && Height > 1)
            {
                int radius = Math.Max(18, (int)Math.Round(18D * DeviceDpi / 96D));
                using (GraphicsPath path = DrawingPaths.RoundedRectangle(
                    new Rectangle(0, 0, Width, Height),
                    radius))
                {
                    Region previous = Region;
                    Region = new Region(path);
                    if (previous != null)
                    {
                        previous.Dispose();
                    }
                }
            }
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Math.Max(18, (int)Math.Round(18D * DeviceDpi / 96D));
            using (GraphicsPath path = DrawingPaths.RoundedRectangle(bounds, radius))
            using (Pen border = new Pen(palette.Border))
            {
                eventArgs.Graphics.DrawPath(border, path);
            }
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.Escape)
            {
                HideSalto();
                return true;
            }

            if (key == Keys.Enter)
            {
                ExecuteSelected();
                return true;
            }

            if (key == Keys.Down)
            {
                MoveSelection(1);
                return true;
            }

            if (key == Keys.Up)
            {
                MoveSelection(-1);
                return true;
            }

            return base.ProcessCmdKey(ref message, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs eventArgs)
        {
            if (!allowClose && eventArgs.CloseReason == CloseReason.UserClosing)
            {
                eventArgs.Cancel = true;
                HideSalto();
                return;
            }

            base.OnFormClosing(eventArgs);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                openingTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        private static Label CreateLabel(
            string text,
            float size,
            FontStyle style,
            Point location,
            Size bounds)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = new Font(
                style == FontStyle.Regular ? "Segoe UI" : "Segoe UI Semibold",
                size,
                style,
                GraphicsUnit.Point);
            label.Location = location;
            label.Size = bounds;
            label.BackColor = Color.Transparent;
            return label;
        }

        private void SearchBoxTextChanged(object sender, EventArgs eventArgs)
        {
            ResetFooterStatus();
            RefreshResults();
        }

        private void ApplySearchCue()
        {
            const int setCueBanner = 0x1501;
            if (searchBox.IsHandleCreated)
            {
                NativeMethods.SendMessage(
                    searchBox.Handle,
                    setCueBanner,
                    new IntPtr(1),
                    "Buscar ventanas, apps, carpetas o calcular");
            }
        }

        private void RefreshResults()
        {
            string query = searchBox.Text ?? string.Empty;
            bool queryEmpty = string.IsNullOrWhiteSpace(query);
            sectionLabel.Text = queryEmpty ? "ACCIONES" : "RESULTADOS";
            IList<RaudoAction> matches = catalog.Search(query);
            string selectedId = SelectedActionIdForTesting;
            resultList.BeginUpdate();
            resultList.Items.Clear();
            foreach (RaudoAction action in matches)
            {
                resultList.Items.Add(action);
            }

            resultList.EndUpdate();
            if (resultList.Items.Count > 0)
            {
                int selection = 0;
                if (!string.IsNullOrWhiteSpace(selectedId))
                {
                    for (int index = 0; index < resultList.Items.Count; index++)
                    {
                        RaudoAction action = resultList.Items[index] as RaudoAction;
                        if (action != null
                            && string.Equals(action.Id, selectedId, StringComparison.Ordinal))
                        {
                            selection = index;
                            break;
                        }
                    }
                }

                resultList.SelectedIndex = selection;
            }

            resultList.Visible = resultList.Items.Count > 0;
            emptyLabel.Visible = resultList.Items.Count == 0;
            if (resultList.Items.Count == 0)
            {
                if (!queryEmpty && applicationsLoading)
                {
                    emptyLabel.Text = "Preparando aplicaciones…";
                }
                else if (!queryEmpty
                    && !string.IsNullOrWhiteSpace(applicationCatalogError))
                {
                    emptyLabel.Text = "No se pudo consultar el catálogo de aplicaciones";
                }
                else
                {
                    emptyLabel.Text = "No hay resultados que coincidan";
                }
            }

            UpdateKeyboardHint();
        }

        private void UpdateKeyboardHint()
        {
            RaudoAction action = resultList.SelectedItem as RaudoAction;
            if (action == null)
            {
                keyboardHintLabel.Text = "Esc  cerrar";
                return;
            }

            string verb;
            switch (action.Kind)
            {
                case RaudoActionKind.Calculation:
                case RaudoActionKind.Conversion:
                    verb = "copiar";
                    break;
                case RaudoActionKind.Window:
                    verb = string.Equals(
                        action.ShortcutHint,
                        "Traer",
                        StringComparison.OrdinalIgnoreCase)
                        ? "traer"
                        : "abrir";
                    break;
                case RaudoActionKind.Application:
                case RaudoActionKind.Folder:
                    verb = "abrir";
                    break;
                default:
                    verb = "ejecutar";
                    break;
            }

            keyboardHintLabel.Text = "↑↓  mover     Enter  " + verb;
        }

        private void MoveSelection(int direction)
        {
            if (resultList.Items.Count == 0)
            {
                return;
            }

            int current = resultList.SelectedIndex;
            if (current < 0)
            {
                current = 0;
            }
            else
            {
                current += direction;
                if (current < 0)
                {
                    current = resultList.Items.Count - 1;
                }
                else if (current >= resultList.Items.Count)
                {
                    current = 0;
                }
            }

            resultList.SelectedIndex = current;
        }

        private void ExecuteSelected()
        {
            RaudoAction action = resultList.SelectedItem as RaudoAction;
            if (action == null)
            {
                return;
            }

            string query = searchBox.Text;
            HideSalto();
            BeginInvoke(new MethodInvoker(delegate
            {
                string error = action.Execute();
                if (!string.IsNullOrWhiteSpace(error) && !IsDisposed)
                {
                    ShowExecutionError(query, error);
                }
            }));
        }

        private void ShowExecutionError(string query, string error)
        {
            catalog.Refresh();
            searchBox.Text = query ?? string.Empty;
            RefreshResults();
            PositionOnForegroundScreen();
            Opacity = 1D;
            if (!Visible)
            {
                Show();
            }

            Activate();
            BringToFront();
            searchBox.Focus();
            executionError = error;
            localLabel.Text = "●  " + error;
            localLabel.ForeColor = palette.Danger;
        }

        private void ResetFooterStatus()
        {
            executionError = null;
            localLabel.Text = "●  Raudo se ejecuta localmente";
            if (palette != null)
            {
                localLabel.ForeColor = palette.TextMuted;
            }
        }

        private void ResultListKeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.KeyCode == Keys.Escape)
            {
                HideSalto();
                eventArgs.Handled = true;
            }
            else if (eventArgs.KeyCode == Keys.Enter)
            {
                ExecuteSelected();
                eventArgs.Handled = true;
            }
        }

        private void ResultListMouseClick(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button != MouseButtons.Left)
            {
                return;
            }

            int index = resultList.IndexFromPoint(eventArgs.Location);
            if (index >= 0)
            {
                resultList.SelectedIndex = index;
                ExecuteSelected();
            }
        }

        private void OpeningTimerTick(object sender, EventArgs eventArgs)
        {
            double progress = Math.Min(
                1D,
                openingWatch.Elapsed.TotalMilliseconds / OpeningDurationMilliseconds);
            Opacity = 1D - Math.Pow(1D - progress, 3D);
            if (progress >= 1D)
            {
                openingTimer.Stop();
                openingWatch.Stop();
                Opacity = 1D;
            }
        }

        private void SaltoVisibleChanged(object sender, EventArgs eventArgs)
        {
            if (!Visible)
            {
                openingTimer.Stop();
                openingWatch.Reset();
                Opacity = 1D;
            }
        }

        private void PositionOnForegroundScreen()
        {
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            Screen screen = foreground == IntPtr.Zero
                ? Screen.FromPoint(Cursor.Position)
                : Screen.FromHandle(foreground);
            Rectangle area = screen.WorkingArea;
            int left = area.Left + ((area.Width - Width) / 2);
            int preferredTop = area.Top + Math.Max(36, (area.Height - Height) / 3);
            int top = Math.Min(
                Math.Max(area.Top + 16, preferredTop),
                area.Bottom - Height - 16);
            Location = new Point(
                Math.Min(Math.Max(area.Left + 16, left), area.Right - Width - 16),
                top);
        }
    }

    internal sealed class SaltoSearchGlyph : Control
    {
        private ThemePalette palette;

        public SaltoSearchGlyph()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(24, 24);
            TabStop = false;
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            ThemePalette colors = palette ?? ThemeService.Current();
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(colors.TextMuted, 1.8F))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                eventArgs.Graphics.DrawEllipse(pen, 3, 3, 12, 12);
                eventArgs.Graphics.DrawLine(pen, 14, 14, 20, 20);
            }
        }
    }

    internal sealed class SaltoResultList : ListBox
    {
        private readonly Font titleFont;
        private readonly Font descriptionFont;
        private readonly Font shortcutFont;
        private ThemePalette palette;

        public SaltoResultList()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 54;
            BorderStyle = BorderStyle.None;
            IntegralHeight = false;
            titleFont = new Font(
                "Segoe UI Semibold",
                9.5F,
                FontStyle.Bold,
                GraphicsUnit.Point);
            descriptionFont = new Font(
                "Segoe UI",
                8.25F,
                FontStyle.Regular,
                GraphicsUnit.Point);
            shortcutFont = new Font(
                "Segoe UI Semibold",
                7.75F,
                FontStyle.Bold,
                GraphicsUnit.Point);
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            BackColor = palette.Window;
            ForeColor = palette.Text;
            Invalidate();
        }

        protected override void OnDrawItem(DrawItemEventArgs eventArgs)
        {
            if (eventArgs.Index < 0 || eventArgs.Index >= Items.Count)
            {
                return;
            }

            ThemePalette colors = palette ?? ThemeService.Current();
            RaudoAction action = Items[eventArgs.Index] as RaudoAction;
            if (action == null)
            {
                return;
            }

            bool selected = (eventArgs.State & DrawItemState.Selected) != 0;
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle row = new Rectangle(
                eventArgs.Bounds.Left + 2,
                eventArgs.Bounds.Top + 2,
                eventArgs.Bounds.Width - 4,
                eventArgs.Bounds.Height - 4);
            Color rowColor = selected ? colors.SurfaceRaised : colors.Window;
            using (GraphicsPath rowPath = DrawingPaths.RoundedRectangle(row, 11))
            using (SolidBrush rowBrush = new SolidBrush(rowColor))
            {
                eventArgs.Graphics.FillPath(rowBrush, rowPath);
            }

            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(colors.Primary))
                {
                    eventArgs.Graphics.FillRectangle(
                        accent,
                        row.Left,
                        row.Top + 12,
                        3,
                        row.Height - 24);
                }
            }

            Rectangle iconTile = new Rectangle(row.Left + 12, row.Top + 7, 36, 36);
            Color iconFill = selected ? colors.Primary : colors.SurfaceRaised;
            Color iconColor = selected ? colors.PrimaryForeground : colors.Primary;
            using (GraphicsPath iconPath = DrawingPaths.RoundedRectangle(iconTile, 10))
            using (SolidBrush iconBrush = new SolidBrush(iconFill))
            {
                eventArgs.Graphics.FillPath(iconBrush, iconPath);
            }

            RaudoActionGlyphDrawing.Draw(
                eventArgs.Graphics,
                new Rectangle(iconTile.Left + 8, iconTile.Top + 8, 20, 20),
                action.Glyph,
                iconColor);

            int textLeft = iconTile.Right + 14;
            int shortcutWidth = string.IsNullOrWhiteSpace(action.ShortcutHint) ? 0 : 126;
            int textRight = row.Right - 14 - shortcutWidth;
            using (SolidBrush titleBrush = new SolidBrush(colors.Text))
            using (SolidBrush descriptionBrush = new SolidBrush(colors.TextMuted))
            using (SolidBrush shortcutBrush = new SolidBrush(colors.TextFaint))
            using (StringFormat oneLine = new StringFormat())
            {
                oneLine.Trimming = StringTrimming.EllipsisCharacter;
                oneLine.FormatFlags = StringFormatFlags.NoWrap;
                eventArgs.Graphics.DrawString(
                    action.Title,
                    titleFont,
                    titleBrush,
                    new RectangleF(textLeft, row.Top + 7, Math.Max(1, textRight - textLeft), 19),
                    oneLine);
                eventArgs.Graphics.DrawString(
                    action.Description,
                    descriptionFont,
                    descriptionBrush,
                    new RectangleF(textLeft, row.Top + 27, Math.Max(1, textRight - textLeft), 17),
                    oneLine);

                if (shortcutWidth > 0)
                {
                    oneLine.Alignment = StringAlignment.Far;
                    oneLine.LineAlignment = StringAlignment.Center;
                    eventArgs.Graphics.DrawString(
                        action.ShortcutHint,
                        shortcutFont,
                        shortcutBrush,
                        new RectangleF(
                            row.Right - shortcutWidth - 14,
                            row.Top,
                            shortcutWidth,
                            row.Height),
                        oneLine);
                }
            }

            if (selected && Focused && ShowFocusCues)
            {
                ControlPaint.DrawFocusRectangle(
                    eventArgs.Graphics,
                    Rectangle.Inflate(row, -4, -4),
                    colors.Primary,
                    rowColor);
            }
        }

        protected override void OnMouseMove(MouseEventArgs eventArgs)
        {
            int index = IndexFromPoint(eventArgs.Location);
            if (index >= 0 && index != SelectedIndex)
            {
                SelectedIndex = index;
            }

            base.OnMouseMove(eventArgs);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                titleFont.Dispose();
                descriptionFont.Dispose();
                shortcutFont.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

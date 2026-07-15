using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Raudo
{
    internal enum SaltoPresentationMode
    {
        Ready,
        Answer,
        Results,
        Loading,
        Empty
    }

    internal sealed class SaltoPositionChangedEventArgs : EventArgs
    {
        public SaltoPositionChangedEventArgs(Point anchor)
        {
            Anchor = anchor;
        }

        public Point Anchor { get; private set; }
    }

    internal sealed class SaltoOpacityChangedEventArgs : EventArgs
    {
        public SaltoOpacityChangedEventArgs(int opacityPercent)
        {
            OpacityPercent = opacityPercent;
        }

        public int OpacityPercent { get; private set; }
    }

    internal sealed class SaltoForm : Form
    {
        private const int OpeningDurationMilliseconds = 140;
        private const int PresentationDurationMilliseconds = 167;
        private const int LoadingIntervalMilliseconds = 333;
        private const int ClipboardLoadingDelayMilliseconds = 120;
        private readonly RaudoActionCatalog catalog;
        private readonly RaudoSettings settings;
        private readonly Func<bool> animationsEnabled;
        private readonly IClipboardHistoryProvider clipboardHistoryProvider;
        private readonly RoundedPanel searchSurface;
        private readonly SaltoSearchGlyph searchGlyph;
        private readonly TextBox searchBox;
        private readonly SaltoOpacityButton opacityButton;
        private readonly Label escapeLabel;
        private readonly Label sectionLabel;
        private readonly SaltoResultList resultList;
        private readonly SaltoScrollIndicator scrollIndicator;
        private readonly Label emptyLabel;
        private readonly Panel footerDivider;
        private readonly Label localLabel;
        private readonly SaltoDragHandle dragHandle;
        private readonly Label keyboardHintLabel;
        private readonly ToolTip toolTip;
        private readonly Timer openingTimer;
        private readonly Stopwatch openingWatch;
        private readonly Timer presentationTimer;
        private readonly Stopwatch presentationWatch;
        private readonly Timer loadingTimer;
        private readonly Timer clipboardLoadingDelayTimer;

        private ThemePalette palette;
        private DpiMetrics dpiMetrics;
        private bool allowClose;
        private bool applicationsLoading;
        private string applicationCatalogError;
        private string executionError;
        private Rectangle presentationStartBounds;
        private Rectangle presentationTargetBounds;
        private SaltoPresentationMode presentationMode;
        private int visibleResultRows;
        private int logicalPresentationWidth = 640;
        private int logicalPresentationHeight = 378;
        private float layoutScale = 1F;
        private int loadingFrame;
        private Point dragStartCursor;
        private Point dragStartLocation;
        private bool dragging;
        private bool applyingAdaptiveLayout;
        private bool adaptiveControlsReady;
        private bool suppressLayoutScaleRefresh;
        private CancellationTokenSource clipboardQueryCancellation;
        private int clipboardQueryGeneration;
        private ClipboardHistorySessionPhase clipboardPhase;
        private ClipboardHistoryQueryStatus clipboardQueryStatus;

        public event EventHandler<SaltoPositionChangedEventArgs> PositionChangedByUser;
        public event EventHandler<SaltoOpacityChangedEventArgs> OpacityChangedByUser;

        public SaltoForm(RaudoActionCatalog actionCatalog)
            : this(actionCatalog, new RaudoSettings(), null, null)
        {
        }

        public SaltoForm(RaudoActionCatalog actionCatalog, RaudoSettings currentSettings)
            : this(actionCatalog, currentSettings, null, null)
        {
        }

        internal SaltoForm(
            RaudoActionCatalog actionCatalog,
            RaudoSettings currentSettings,
            Func<bool> motionSetting)
            : this(actionCatalog, currentSettings, motionSetting, null)
        {
        }

        internal SaltoForm(
            RaudoActionCatalog actionCatalog,
            RaudoSettings currentSettings,
            Func<bool> motionSetting,
            IClipboardHistoryProvider historyProvider)
        {
            if (actionCatalog == null)
            {
                throw new ArgumentNullException("actionCatalog");
            }

            if (currentSettings == null)
            {
                throw new ArgumentNullException("currentSettings");
            }

            catalog = actionCatalog;
            settings = currentSettings;
            animationsEnabled = motionSetting ?? MotionSettings.ClientAreaAnimationsEnabled;
            clipboardHistoryProvider = historyProvider ?? new ClipboardHistoryProvider();
            settings.Normalize();
            dpiMetrics = DpiMetrics.ForDpi(DpiMetrics.DefaultDpi);
            Text = "Salto · Raudo";
            AccessibleName = "Salto de Raudo";
            AccessibleDescription =
                "Busca ventanas, aplicaciones y carpetas, o calcula localmente";
            ClientSize = new Size(640, 378);
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
            searchBox.Size = new Size(430, 24);
            searchBox.TabIndex = 0;
            searchBox.AccessibleName = "Buscar en Salto";
            searchBox.AccessibleDescription =
                "Escribe para buscar ventanas, aplicaciones y carpetas, o calcular";
            searchBox.TextChanged += SearchBoxTextChanged;
            searchBox.HandleCreated += delegate { ApplySearchCue(); };
            searchSurface.Controls.Add(searchBox);

            opacityButton = new SaltoOpacityButton();
            opacityButton.Location = new Point(506, 12);
            opacityButton.Size = new Size(36, 32);
            opacityButton.TabIndex = 2;
            opacityButton.AccessibleName = "Opacidad de Salto";
            opacityButton.Click += delegate { CycleOpacity(); };
            searchSurface.Controls.Add(opacityButton);

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
            resultList.SelectedIndexChanged += delegate
            {
                UpdateKeyboardHint();
                UpdateScrollIndicator();
            };
            resultList.ViewportChanged += delegate { UpdateScrollIndicator(); };
            Controls.Add(resultList);

            scrollIndicator = new SaltoScrollIndicator();
            scrollIndicator.Location = new Point(619, 112);
            scrollIndicator.Size = new Size(3, 260);
            scrollIndicator.TabStop = false;
            Controls.Add(scrollIndicator);

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
            footerDivider.Location = new Point(16, 336);
            footerDivider.Size = new Size(608, 1);
            Controls.Add(footerDivider);

            localLabel = CreateLabel(
                "●  Raudo se ejecuta localmente",
                8F,
                FontStyle.Regular,
                new Point(21, 346),
                new Size(220, 22));
            Controls.Add(localLabel);

            dragHandle = new SaltoDragHandle();
            dragHandle.Location = new Point(284, 346);
            dragHandle.Size = new Size(72, 22);
            dragHandle.TabStop = false;
            dragHandle.AccessibleName = "Mover Salto";
            dragHandle.AccessibleDescription =
                "Arrastra para mover. Haz doble clic para centrar en esta pantalla.";
            dragHandle.MouseDown += DragHandleMouseDown;
            dragHandle.MouseMove += DragHandleMouseMove;
            dragHandle.MouseUp += DragHandleMouseUp;
            dragHandle.DoubleClick += delegate { CenterOnForegroundScreen(true); };
            Controls.Add(dragHandle);

            keyboardHintLabel = CreateLabel(
                "↑↓  mover     Enter  abrir",
                8F,
                FontStyle.Regular,
                new Point(368, 346),
                new Size(252, 22));
            keyboardHintLabel.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(keyboardHintLabel);

            openingTimer = new Timer();
            openingTimer.Interval = 15;
            openingTimer.Tick += OpeningTimerTick;
            openingWatch = new Stopwatch();

            presentationTimer = new Timer();
            presentationTimer.Interval = 15;
            presentationTimer.Tick += PresentationTimerTick;
            presentationWatch = new Stopwatch();

            loadingTimer = new Timer();
            loadingTimer.Interval = LoadingIntervalMilliseconds;
            loadingTimer.Tick += LoadingTimerTick;

            clipboardLoadingDelayTimer = new Timer();
            clipboardLoadingDelayTimer.Interval = ClipboardLoadingDelayMilliseconds;
            clipboardLoadingDelayTimer.Tick += ClipboardLoadingDelayTimerTick;

            toolTip = new ToolTip();
            toolTip.AutoPopDelay = 6000;
            toolTip.InitialDelay = 450;
            toolTip.ReshowDelay = 100;
            toolTip.SetToolTip(opacityButton, "Cambiar opacidad · Ctrl + Shift + O");
            toolTip.SetToolTip(dragHandle, "Arrastra para mover · doble clic para centrar");

            Deactivate += delegate { HideSalto(); };
            VisibleChanged += SaltoVisibleChanged;
            presentationMode = SaltoPresentationMode.Ready;
            visibleResultRows = 4;
            ApplyTheme(ThemeService.Current());
            adaptiveControlsReady = true;
            ApplyAdaptiveLayout();
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
            opacityButton.ApplyTheme(palette);
            resultList.ApplyTheme(palette);
            scrollIndicator.ApplyTheme(palette);
            dragHandle.ApplyTheme(palette);
            opacityButton.Enabled = !palette.IsHighContrast;
            opacityButton.Visible = !palette.IsHighContrast;
            ApplyEffectiveOpacity();
            ApplyAdaptiveLayout();
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

            bool animate = AnimationsEnabled();
            Opacity = animate ? 0D : GetEffectiveOpacity();
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
            CancelClipboardQuery(true);
            openingTimer.Stop();
            openingWatch.Reset();
            presentationTimer.Stop();
            presentationWatch.Reset();
            loadingTimer.Stop();
            Opacity = GetEffectiveOpacity();
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
            presentationTimer.Stop();
            loadingTimer.Stop();
            Close();
        }

        public void SetApplicationCatalogState(bool loading, string error)
        {
            applicationsLoading = loading;
            applicationCatalogError = error;
            UpdateLoadingPresentation();
            if (Visible && !ClipboardModeActive)
            {
                RefreshResults();
            }
        }

        public void RefreshCatalog()
        {
            catalog.Refresh();
            if (!ClipboardModeActive)
            {
                RefreshResults();
            }
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
            get { return openingTimer.Enabled || presentationTimer.Enabled; }
        }

        internal SaltoPresentationMode PresentationModeForTesting
        {
            get { return presentationMode; }
        }

        internal int VisibleRowsForTesting
        {
            get { return visibleResultRows; }
        }

        internal int OpacityPercentForTesting
        {
            get { return settings.SaltoOpacityPercent; }
        }

        internal double EffectiveOpacityForTesting
        {
            get { return GetEffectiveOpacity(); }
        }

        internal bool LoadingAnimationRunningForTesting
        {
            get { return loadingTimer.Enabled; }
        }

        internal bool ScrollIndicatorVisibleForTesting
        {
            get { return resultList.Items.Count > visibleResultRows; }
        }

        internal string LoadingTextForTesting
        {
            get { return emptyLabel.Text; }
        }

        internal Rectangle PresentationStartBoundsForTesting
        {
            get { return presentationStartBounds; }
        }

        internal Rectangle PresentationTargetBoundsForTesting
        {
            get { return presentationTargetBounds; }
        }

        internal string KeyboardHintForTesting
        {
            get { return keyboardHintLabel.Text; }
        }

        internal void SetQueryForTesting(string query)
        {
            string value = query ?? string.Empty;
            if (!string.Equals(searchBox.Text, value, StringComparison.Ordinal))
            {
                searchBox.Text = value;
            }
            else
            {
                RefreshResults();
            }
        }

        internal void MoveSelectionForTesting(int direction)
        {
            MoveSelection(direction);
        }

        internal void ExecuteSelectedForTesting()
        {
            ExecuteSelected();
        }

        internal void CycleOpacityForTesting()
        {
            CycleOpacity();
        }

        internal void CenterForTesting()
        {
            CenterOnForegroundScreen(false);
        }

        internal void MoveForTesting(Point location)
        {
            Location = ClampWindowBounds(new Rectangle(location, Size)).Location;
            NotifyPositionChanged();
        }

        internal void ApplyPresentationProgressForTesting(double progress)
        {
            presentationTimer.Stop();
            presentationWatch.Stop();
            double bounded = Math.Max(0D, Math.Min(1D, progress));
            double eased = 1D - Math.Pow(1D - bounded, 3D);
            suppressLayoutScaleRefresh = true;
            try
            {
                Bounds = InterpolateBounds(
                    presentationStartBounds,
                    presentationTargetBounds,
                    eased);
            }
            finally
            {
                suppressLayoutScaleRefresh = false;
            }
        }

        internal RaudoActionKind? SelectedActionKindForTesting
        {
            get
            {
                RaudoAction selected = resultList.SelectedItem as RaudoAction;
                return selected == null ? (RaudoActionKind?)null : selected.Kind;
            }
        }

        internal bool ClipboardModeForTesting
        {
            get { return ClipboardModeActive; }
        }

        internal bool ClipboardQueryPendingForTesting
        {
            get { return ClipboardQueryPending; }
        }

        internal ClipboardHistoryQueryStatus ClipboardStatusForTesting
        {
            get { return clipboardQueryStatus; }
        }

        internal void ShowClipboardLoadingForTesting()
        {
            ClipboardLoadingDelayTimerTick(this, EventArgs.Empty);
        }

        internal void ApplyDpiChangeForTesting(int dpi, Rectangle suggestedBounds)
        {
            ApplyDpiChange(dpi, suggestedBounds);
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
            dpiMetrics = DpiMetrics.FromControl(this);
            layoutScale = dpiMetrics.ScaleFactor;
            WindowTheme.Apply(Handle, palette == null ? false : palette.IsDark);
        }

        protected override void OnResize(EventArgs eventArgs)
        {
            base.OnResize(eventArgs);
            if (adaptiveControlsReady)
            {
                ApplyAdaptiveLayout();
            }

            if (Width > 1 && Height > 1)
            {
                int radius = dpiMetrics.Scale(18);
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
            int radius = dpiMetrics.Scale(18);
            using (GraphicsPath path = DrawingPaths.RoundedRectangle(bounds, radius))
            using (Pen border = new Pen(palette.Border))
            {
                eventArgs.Graphics.DrawPath(border, path);
            }
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            Keys modifiers = keyData & Keys.Modifiers;
            if (key == Keys.O
                && modifiers == (Keys.Control | Keys.Shift))
            {
                CycleOpacity();
                return true;
            }

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

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == DpiMessage.WindowDpiChanged)
            {
                int nextDpi = DpiMessage.GetDpi(message);
                Rectangle suggestedBounds = DpiMessage.GetSuggestedBounds(message);
                presentationTimer.Stop();
                presentationWatch.Reset();
                dpiMetrics = DpiMetrics.ForDpi(nextDpi);
                layoutScale = dpiMetrics.ScaleFactor;
                base.WndProc(ref message);
                ApplyDpiChange(nextDpi, suggestedBounds);
                return;
            }

            base.WndProc(ref message);
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
                CancelClipboardQuery(true);
                openingTimer.Dispose();
                presentationTimer.Dispose();
                loadingTimer.Dispose();
                clipboardLoadingDelayTimer.Dispose();
                toolTip.Dispose();
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
            string filter;
            if (ClipboardHistoryQuery.TryParse(searchBox.Text, out filter))
            {
                BeginClipboardQuery(filter);
                return;
            }

            CancelClipboardQuery(true);
            RefreshResults();
        }

        private async void BeginClipboardQuery(string filter)
        {
            CancelClipboardQuery(true);
            clipboardPhase = ClipboardHistorySessionPhase.Pending;
            clipboardQueryStatus = ClipboardHistoryQueryStatus.Success;
            int generation = ++clipboardQueryGeneration;
            clipboardQueryCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = clipboardQueryCancellation.Token;
            clipboardLoadingDelayTimer.Start();

            resultList.BeginUpdate();
            resultList.Items.Clear();
            resultList.EndUpdate();
            resultList.Visible = false;
            emptyLabel.Visible = false;
            UpdateKeyboardHint();
            UpdateScrollIndicator();

            try
            {
                ClipboardHistoryQueryResult result = await clipboardHistoryProvider
                    .QueryAsync(filter, 5, cancellationToken);
                if (cancellationToken.IsCancellationRequested
                    || generation != clipboardQueryGeneration
                    || IsDisposed)
                {
                    return;
                }

                clipboardLoadingDelayTimer.Stop();
                clipboardPhase = ClipboardHistorySessionPhase.Results;
                ReleaseCompletedClipboardQuery();
                ApplyClipboardResult(result, filter, generation);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                if (!cancellationToken.IsCancellationRequested
                    && generation == clipboardQueryGeneration
                    && !IsDisposed)
                {
                    clipboardLoadingDelayTimer.Stop();
                    clipboardPhase = ClipboardHistorySessionPhase.Results;
                    ReleaseCompletedClipboardQuery();
                    ApplyClipboardResult(
                        new ClipboardHistoryQueryResult(
                            ClipboardHistoryQueryStatus.Failed,
                            new List<ClipboardHistoryEntry>()),
                        filter,
                        generation);
                }
            }
        }

        private void ClipboardLoadingDelayTimerTick(object sender, EventArgs eventArgs)
        {
            clipboardLoadingDelayTimer.Stop();
            if (clipboardPhase != ClipboardHistorySessionPhase.Pending)
            {
                return;
            }

            clipboardPhase = ClipboardHistorySessionPhase.Loading;
            resultList.Visible = false;
            emptyLabel.Visible = true;
            emptyLabel.Text = ClipboardLoadingText();
            sectionLabel.Text = "PORTAPAPELES";
            presentationMode = SaltoPresentationMode.Loading;
            visibleResultRows = 1;
            TransitionToPresentation(560, 216);
            UpdateKeyboardHint();
            UpdateLoadingPresentation();
        }

        private void ApplyClipboardResult(
            ClipboardHistoryQueryResult result,
            string filter,
            int generation)
        {
            clipboardPhase = ClipboardHistorySessionPhase.Results;
            clipboardQueryStatus = result.Status;
            resultList.BeginUpdate();
            resultList.Items.Clear();
            if (result.Status == ClipboardHistoryQueryStatus.Success)
            {
                int count = Math.Min(5, result.Entries.Count);
                for (int index = 0; index < count; index++)
                {
                    ClipboardHistoryEntry entry = result.Entries[index];
                    string text = entry.Text;
                    resultList.Items.Add(new RaudoAction(
                        "clipboard." + generation + "." + index,
                        entry.Preview,
                        "Texto reciente del historial de Windows",
                        filter,
                        "Copiar",
                        RaudoActionGlyph.Clipboard,
                        RaudoActionKind.Clipboard,
                        false,
                        0,
                        delegate { return ClipboardWriter.TryCopy(text); }));
                }
            }
            else if (result.Status == ClipboardHistoryQueryStatus.Disabled)
            {
                resultList.Items.Add(new RaudoAction(
                    "clipboard.settings",
                    "Activar historial del portapapeles",
                    "Abre Configuración > Sistema > Portapapeles",
                    string.Empty,
                    "Abrir",
                    RaudoActionGlyph.Clipboard,
                    RaudoActionKind.Settings,
                    false,
                    0,
                    WindowsSettingsLauncher.TryOpenClipboard));
            }

            resultList.EndUpdate();
            bool hasItems = resultList.Items.Count > 0;
            resultList.Visible = hasItems;
            emptyLabel.Visible = !hasItems;
            sectionLabel.Text = "PORTAPAPELES";
            if (hasItems)
            {
                resultList.SelectedIndex = 0;
                presentationMode = SaltoPresentationMode.Results;
                visibleResultRows = Math.Min(5, resultList.Items.Count);
                TransitionToPresentation(640, 162 + (54 * visibleResultRows));
            }
            else
            {
                emptyLabel.Text = ClipboardEmptyText(result.Status, filter);
                presentationMode = SaltoPresentationMode.Empty;
                visibleResultRows = 1;
                TransitionToPresentation(560, 216);
            }

            UpdateKeyboardHint();
            resultList.HideNativeScrollBar();
            UpdateScrollIndicator();
            UpdateLoadingPresentation();
        }

        private void CancelClipboardQuery(bool clearResults)
        {
            clipboardLoadingDelayTimer.Stop();
            clipboardQueryGeneration++;
            if (clipboardQueryCancellation != null)
            {
                clipboardQueryCancellation.Cancel();
                clipboardQueryCancellation.Dispose();
                clipboardQueryCancellation = null;
            }

            bool wasClipboardMode = ClipboardModeActive;
            clipboardPhase = ClipboardHistorySessionPhase.Inactive;
            if (clearResults && wasClipboardMode && resultList != null)
            {
                resultList.Items.Clear();
            }

            UpdateLoadingPresentation();
        }

        private void ReleaseCompletedClipboardQuery()
        {
            if (clipboardQueryCancellation != null)
            {
                clipboardQueryCancellation.Dispose();
                clipboardQueryCancellation = null;
            }
        }

        private bool ClipboardModeActive
        {
            get { return clipboardPhase != ClipboardHistorySessionPhase.Inactive; }
        }

        private bool ClipboardQueryPending
        {
            get
            {
                return clipboardPhase == ClipboardHistorySessionPhase.Pending
                    || clipboardPhase == ClipboardHistorySessionPhase.Loading;
            }
        }

        private bool ClipboardLoadingVisible
        {
            get { return clipboardPhase == ClipboardHistorySessionPhase.Loading; }
        }

        private static string ClipboardEmptyText(
            ClipboardHistoryQueryStatus status,
            string filter)
        {
            switch (status)
            {
                case ClipboardHistoryQueryStatus.Disabled:
                    return "El historial del portapapeles está desactivado";
                case ClipboardHistoryQueryStatus.AccessDenied:
                    return "Windows bloqueó el acceso al historial";
                case ClipboardHistoryQueryStatus.Unavailable:
                    return "El historial no está disponible en esta versión de Windows";
                case ClipboardHistoryQueryStatus.Failed:
                    return "Windows no pudo consultar el historial";
                default:
                    return string.IsNullOrWhiteSpace(filter)
                        ? "No hay textos recientes en el historial"
                        : "No hay textos recientes que coincidan";
            }
        }

        private string ClipboardLoadingText()
        {
            return AnimationsEnabled()
                ? "Consultando historial" + new string('·', loadingFrame + 1)
                : "Consultando historial…";
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
            if (ClipboardModeActive)
            {
                return;
            }

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
            resultList.HideNativeScrollBar();
            UpdatePresentation(queryEmpty);
            UpdateScrollIndicator();
            UpdateLoadingPresentation();
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
                case RaudoActionKind.Clipboard:
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
                case RaudoActionKind.Settings:
                case RaudoActionKind.Folder:
                    verb = "abrir";
                    break;
                case RaudoActionKind.Media:
                    verb = "controlar";
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
            Opacity = GetEffectiveOpacity();
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
            UpdateFooterText();
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

        protected override void OnLayout(LayoutEventArgs eventArgs)
        {
            base.OnLayout(eventArgs);
            if (!applyingAdaptiveLayout && adaptiveControlsReady)
            {
                ApplyAdaptiveLayout();
            }
        }

        private void UpdatePresentation(bool queryEmpty)
        {
            int resultCount = resultList.Items.Count;
            int logicalWidth;
            int rows;
            RaudoAction first = resultCount == 0
                ? null
                : resultList.Items[0] as RaudoAction;

            if (queryEmpty)
            {
                presentationMode = SaltoPresentationMode.Ready;
                logicalWidth = 640;
                rows = Math.Max(1, Math.Min(4, resultCount));
                sectionLabel.Text = "ACCIONES";
            }
            else if (resultCount == 0 && IsArithmeticIntent(searchBox.Text))
            {
                presentationMode = SaltoPresentationMode.Answer;
                logicalWidth = 520;
                rows = 1;
                sectionLabel.Text = "CÁLCULO";
                emptyLabel.Text = "Continúa escribiendo la operación";
            }
            else if (resultCount == 0)
            {
                presentationMode = applicationsLoading
                    ? SaltoPresentationMode.Loading
                    : SaltoPresentationMode.Empty;
                logicalWidth = 560;
                rows = 1;
                sectionLabel.Text = applicationsLoading ? "PREPARANDO" : "SIN RESULTADOS";
            }
            else if (first != null
                && (first.Kind == RaudoActionKind.Calculation
                    || first.Kind == RaudoActionKind.Conversion))
            {
                presentationMode = SaltoPresentationMode.Answer;
                logicalWidth = 520;
                rows = 1;
                sectionLabel.Text = "RESULTADO";
            }
            else
            {
                presentationMode = SaltoPresentationMode.Results;
                logicalWidth = 640;
                rows = Math.Max(1, Math.Min(5, resultCount));
                sectionLabel.Text = resultCount == 1 ? "RESULTADO" : "RESULTADOS";
            }

            visibleResultRows = rows;
            int logicalHeight = 162 + (54 * rows);
            TransitionToPresentation(logicalWidth, logicalHeight);
        }

        private static bool IsArithmeticIntent(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length > 128)
            {
                return false;
            }

            bool hasDigit = false;
            for (int index = 0; index < query.Length; index++)
            {
                char character = query[index];
                if (char.IsDigit(character))
                {
                    hasDigit = true;
                    continue;
                }

                if (char.IsWhiteSpace(character)
                    || character == '.'
                    || character == ','
                    || character == '+'
                    || character == '-'
                    || character == '*'
                    || character == '/'
                    || character == '%'
                    || character == '('
                    || character == ')')
                {
                    continue;
                }

                return false;
            }

            return hasDigit;
        }

        private void TransitionToPresentation(int logicalWidth, int logicalHeight)
        {
            RefreshLayoutScaleFromBounds();
            float scale = GetLayoutScale();
            logicalPresentationWidth = logicalWidth;
            logicalPresentationHeight = logicalHeight;
            Size targetSize = new Size(
                Math.Max(1, (int)Math.Round(logicalWidth * scale)),
                Math.Max(1, (int)Math.Round(logicalHeight * scale)));

            if (!Visible)
            {
                presentationTimer.Stop();
                presentationWatch.Reset();
                ClientSize = targetSize;
                ApplyAdaptiveLayout();
                return;
            }

            int centerX = Left + (Width / 2);
            Rectangle target = ClampWindowBounds(
                new Rectangle(
                    centerX - (targetSize.Width / 2),
                    Top,
                    targetSize.Width,
                    targetSize.Height));
            if (Bounds == target)
            {
                presentationTimer.Stop();
                presentationWatch.Reset();
                ApplyAdaptiveLayout();
                return;
            }

            if (!AnimationsEnabled())
            {
                presentationTimer.Stop();
                presentationWatch.Reset();
                Bounds = target;
                ApplyAdaptiveLayout();
                return;
            }

            presentationTimer.Stop();
            presentationStartBounds = Bounds;
            presentationTargetBounds = target;
            presentationWatch.Restart();
            presentationTimer.Start();
        }

        private void ApplyDpiChange(int dpi, Rectangle suggestedBounds)
        {
            presentationTimer.Stop();
            presentationWatch.Reset();
            dpiMetrics = DpiMetrics.ForDpi(dpi);
            layoutScale = dpiMetrics.ScaleFactor;

            Rectangle reference = suggestedBounds.IsEmpty ? Bounds : suggestedBounds;
            Screen destination = Screen.FromRectangle(reference);
            Size targetSize = dpiMetrics.Scale(new Size(
                logicalPresentationWidth,
                logicalPresentationHeight));
            Rectangle target = ClampWindowBounds(
                new Rectangle(
                    reference.Left + ((reference.Width - targetSize.Width) / 2),
                    reference.Top,
                    targetSize.Width,
                    targetSize.Height),
                destination.WorkingArea);

            suppressLayoutScaleRefresh = true;
            try
            {
                Bounds = target;
            }
            finally
            {
                suppressLayoutScaleRefresh = false;
            }

            ApplyAdaptiveLayout();
            Invalidate(true);
        }

        private void PresentationTimerTick(object sender, EventArgs eventArgs)
        {
            double progress = Math.Min(
                1D,
                presentationWatch.Elapsed.TotalMilliseconds
                    / PresentationDurationMilliseconds);
            double eased = 1D - Math.Pow(1D - progress, 3D);
            Bounds = InterpolateBounds(
                presentationStartBounds,
                presentationTargetBounds,
                eased);

            if (progress >= 1D)
            {
                presentationTimer.Stop();
                presentationWatch.Stop();
                Bounds = presentationTargetBounds;
            }
        }

        private static Rectangle InterpolateBounds(
            Rectangle start,
            Rectangle target,
            double progress)
        {
            return new Rectangle(
                Interpolate(start.X, target.X, progress),
                Interpolate(start.Y, target.Y, progress),
                Interpolate(start.Width, target.Width, progress),
                Interpolate(start.Height, target.Height, progress));
        }

        private static int Interpolate(int start, int target, double progress)
        {
            return (int)Math.Round(start + ((target - start) * progress));
        }

        private void ApplyAdaptiveLayout()
        {
            if (applyingAdaptiveLayout
                || !adaptiveControlsReady
                || ClientSize.Width <= 1
                || ClientSize.Height <= 1)
            {
                return;
            }

            applyingAdaptiveLayout = true;
            try
            {
            RefreshLayoutScaleFromBounds();
            float scale = GetLayoutScale();
            int margin = ScaleLogical(16, scale);
            int searchHeight = ScaleLogical(56, scale);
            int searchWidth = Math.Max(ScaleLogical(300, scale), ClientSize.Width - (margin * 2));
            searchSurface.SetBounds(margin, margin, searchWidth, searchHeight);

            searchGlyph.SetBounds(
                ScaleLogical(17, scale),
                ScaleLogical(16, scale),
                ScaleLogical(24, scale),
                ScaleLogical(24, scale));
            int escapeWidth = ScaleLogical(42, scale);
            int escapeLeft = searchWidth - ScaleLogical(60, scale);
            escapeLabel.SetBounds(
                escapeLeft,
                ScaleLogical(16, scale),
                escapeWidth,
                ScaleLogical(24, scale));
            int opacityWidth = ScaleLogical(36, scale);
            int opacityLeft = escapeLeft - ScaleLogical(42, scale);
            opacityButton.SetBounds(
                opacityLeft,
                ScaleLogical(12, scale),
                opacityWidth,
                ScaleLogical(32, scale));
            int searchLeft = ScaleLogical(52, scale);
            int searchRight = opacityButton.Visible ? opacityLeft : escapeLeft;
            searchBox.SetBounds(
                searchLeft,
                ScaleLogical(17, scale),
                Math.Max(ScaleLogical(120, scale), searchRight - searchLeft - ScaleLogical(12, scale)),
                ScaleLogical(24, scale));

            int sectionTop = ScaleLogical(84, scale);
            sectionLabel.SetBounds(
                ScaleLogical(20, scale),
                sectionTop,
                ClientSize.Width - ScaleLogical(40, scale),
                ScaleLogical(20, scale));

            int footerTop = ClientSize.Height - ScaleLogical(42, scale);
            int resultTop = ScaleLogical(106, scale);
            int resultHeight = Math.Max(
                ScaleLogical(56, scale),
                footerTop - resultTop - ScaleLogical(12, scale));
            int listLeft = margin;
            int indicatorWidth = Math.Max(3, ScaleLogical(3, scale));
            int indicatorLeft = ClientSize.Width - margin - indicatorWidth - ScaleLogical(2, scale);
            int listWidth = Math.Max(
                ScaleLogical(240, scale),
                indicatorLeft - listLeft - ScaleLogical(5, scale));
            resultList.SetBounds(listLeft, resultTop, listWidth, resultHeight);
            resultList.ItemHeight = ScaleLogical(54, scale);
            resultList.HideNativeScrollBar();
            scrollIndicator.SetBounds(
                indicatorLeft,
                resultTop + ScaleLogical(6, scale),
                indicatorWidth,
                Math.Max(1, resultHeight - ScaleLogical(12, scale)));
            emptyLabel.SetBounds(
                ScaleLogical(20, scale),
                resultTop,
                ClientSize.Width - ScaleLogical(40, scale),
                resultHeight);

            footerDivider.SetBounds(
                margin,
                footerTop,
                ClientSize.Width - (margin * 2),
                Math.Max(1, ScaleLogical(1, scale)));
            int footerContentTop = footerTop + ScaleLogical(10, scale);
            int handleWidth = ScaleLogical(72, scale);
            int handleLeft = (ClientSize.Width - handleWidth) / 2;
            dragHandle.SetBounds(
                handleLeft,
                footerContentTop,
                handleWidth,
                ScaleLogical(22, scale));
            localLabel.SetBounds(
                ScaleLogical(21, scale),
                footerContentTop,
                Math.Max(1, handleLeft - ScaleLogical(33, scale)),
                ScaleLogical(22, scale));
            int hintLeft = handleLeft + handleWidth + ScaleLogical(12, scale);
            keyboardHintLabel.SetBounds(
                hintLeft,
                footerContentTop,
                Math.Max(1, ClientSize.Width - hintLeft - ScaleLogical(20, scale)),
                ScaleLogical(22, scale));

            UpdateFooterText();
            UpdateScrollIndicator();
            scrollIndicator.BringToFront();
            }
            finally
            {
                applyingAdaptiveLayout = false;
            }
        }

        private float GetLayoutScale()
        {
            return Math.Max(0.75F, Math.Min(4F, layoutScale));
        }

        private void RefreshLayoutScaleFromBounds()
        {
            if (presentationTimer != null && presentationTimer.Enabled)
            {
                return;
            }

            if (suppressLayoutScaleRefresh)
            {
                return;
            }

            if (logicalPresentationWidth <= 0 || logicalPresentationHeight <= 0)
            {
                return;
            }

            float widthScale = ClientSize.Width / (float)logicalPresentationWidth;
            float heightScale = ClientSize.Height / (float)logicalPresentationHeight;
            float candidate = Math.Min(widthScale, heightScale);
            if (candidate >= 0.75F && candidate <= 4F)
            {
                layoutScale = candidate;
            }
        }

        private static int ScaleLogical(int value, float scale)
        {
            return Math.Max(1, (int)Math.Round(value * scale));
        }

        private void UpdateScrollIndicator()
        {
            if (scrollIndicator == null || resultList == null)
            {
                return;
            }

            scrollIndicator.SetState(
                resultList.Items.Count,
                Math.Max(1, visibleResultRows),
                resultList.Items.Count == 0 ? 0 : resultList.TopIndex);
        }

        private void UpdateLoadingPresentation()
        {
            if (loadingTimer == null)
            {
                return;
            }

            bool loadingVisible = ClipboardLoadingVisible
                || (applicationsLoading && !ClipboardModeActive);
            bool animate = Visible
                && loadingVisible
                && presentationMode != SaltoPresentationMode.Answer
                && AnimationsEnabled();
            if (animate)
            {
                if (!loadingTimer.Enabled)
                {
                    loadingFrame = 0;
                    loadingTimer.Start();
                }
            }
            else
            {
                loadingTimer.Stop();
                loadingFrame = 2;
            }

            UpdateFooterText();
            if (presentationMode == SaltoPresentationMode.Loading)
            {
                emptyLabel.Text = ClipboardLoadingVisible
                    ? ClipboardLoadingText()
                    : LoadingText();
            }
        }

        private void LoadingTimerTick(object sender, EventArgs eventArgs)
        {
            loadingFrame = (loadingFrame + 1) % 3;
            UpdateFooterText();
            if (presentationMode == SaltoPresentationMode.Loading)
            {
                emptyLabel.Text = ClipboardLoadingVisible
                    ? ClipboardLoadingText()
                    : LoadingText();
            }
        }

        private string LoadingText()
        {
            if (!AnimationsEnabled())
            {
                return "Preparando aplicaciones…";
            }

            return "Preparando aplicaciones" + new string('·', loadingFrame + 1);
        }

        private void UpdateFooterText()
        {
            if (localLabel == null || palette == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(executionError))
            {
                localLabel.Text = "●  " + executionError;
                localLabel.ForeColor = palette.Danger;
            }
            else if (ClipboardModeActive)
            {
                localLabel.Text = "●  No se guarda en Raudo";
                localLabel.ForeColor = palette.TextMuted;
            }
            else if (applicationsLoading
                && presentationMode != SaltoPresentationMode.Answer
                && presentationMode != SaltoPresentationMode.Loading)
            {
                localLabel.Text = "●  " + LoadingText();
                localLabel.ForeColor = palette.TextMuted;
            }
            else
            {
                localLabel.Text = ClientSize.Width < ScaleLogical(600, GetLayoutScale())
                    ? "●  Local"
                    : "●  Raudo se ejecuta localmente";
                localLabel.ForeColor = palette.TextMuted;
            }
        }

        private void CycleOpacity()
        {
            if (palette != null && palette.IsHighContrast)
            {
                return;
            }

            if (settings.SaltoOpacityPercent >= 100)
            {
                settings.SaltoOpacityPercent = 82;
            }
            else if (settings.SaltoOpacityPercent >= 82)
            {
                settings.SaltoOpacityPercent = 64;
            }
            else
            {
                settings.SaltoOpacityPercent = 100;
            }

            ApplyEffectiveOpacity();
            EventHandler<SaltoOpacityChangedEventArgs> handler = OpacityChangedByUser;
            if (handler != null)
            {
                handler(this, new SaltoOpacityChangedEventArgs(settings.SaltoOpacityPercent));
            }
        }

        private void ApplyEffectiveOpacity()
        {
            if (opacityButton == null)
            {
                return;
            }

            opacityButton.SetOpacityPercent(
                palette != null && palette.IsHighContrast
                    ? 100
                    : settings.SaltoOpacityPercent);
            if (Visible && !openingTimer.Enabled)
            {
                Opacity = GetEffectiveOpacity();
            }
        }

        private double GetEffectiveOpacity()
        {
            return palette != null && palette.IsHighContrast
                ? 1D
                : Math.Max(0.64D, Math.Min(1D, settings.SaltoOpacityPercent / 100D));
        }

        private bool AnimationsEnabled()
        {
            try
            {
                return animationsEnabled();
            }
            catch (Exception)
            {
                return true;
            }
        }

        private void DragHandleMouseDown(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button != MouseButtons.Left)
            {
                return;
            }

            presentationTimer.Stop();
            dragStartCursor = Cursor.Position;
            dragStartLocation = Location;
            dragging = false;
            dragHandle.Capture = true;
        }

        private void DragHandleMouseMove(object sender, MouseEventArgs eventArgs)
        {
            if ((Control.MouseButtons & MouseButtons.Left) != MouseButtons.Left
                || !dragHandle.Capture)
            {
                return;
            }

            Point cursor = Cursor.Position;
            int deltaX = cursor.X - dragStartCursor.X;
            int deltaY = cursor.Y - dragStartCursor.Y;
            if (!dragging && Math.Abs(deltaX) + Math.Abs(deltaY) < 4)
            {
                return;
            }

            dragging = true;
            Rectangle requested = new Rectangle(
                dragStartLocation.X + deltaX,
                dragStartLocation.Y + deltaY,
                Width,
                Height);
            Location = ClampWindowBounds(requested).Location;
        }

        private void DragHandleMouseUp(object sender, MouseEventArgs eventArgs)
        {
            if (eventArgs.Button != MouseButtons.Left)
            {
                return;
            }

            dragHandle.Capture = false;
            if (dragging)
            {
                NotifyPositionChanged();
            }

            dragging = false;
        }

        private void NotifyPositionChanged()
        {
            Point anchor = new Point(Left + (Width / 2), Top);
            settings.SaltoCenterX = anchor.X;
            settings.SaltoTopY = anchor.Y;
            EventHandler<SaltoPositionChangedEventArgs> handler = PositionChangedByUser;
            if (handler != null)
            {
                handler(this, new SaltoPositionChangedEventArgs(anchor));
            }
        }

        private void CenterOnForegroundScreen(bool notify)
        {
            Screen screen = GetForegroundScreen();
            Rectangle area = screen.WorkingArea;
            int left = area.Left + ((area.Width - Width) / 2);
            int preferredTop = area.Top + Math.Max(36, (area.Height - Height) / 3);
            Bounds = ClampWindowBounds(new Rectangle(left, preferredTop, Width, Height), area);
            if (notify)
            {
                NotifyPositionChanged();
            }
        }

        private Rectangle ClampWindowBounds(Rectangle requested)
        {
            Screen screen = Screen.FromPoint(new Point(
                requested.Left + (requested.Width / 2),
                requested.Top + Math.Min(requested.Height / 2, 80)));
            return ClampWindowBounds(requested, screen.WorkingArea);
        }

        private Rectangle ClampWindowBounds(Rectangle requested, Rectangle area)
        {
            int margin = dpiMetrics.Scale(16);
            int left = Math.Min(
                Math.Max(area.Left + margin, requested.Left),
                Math.Max(area.Left + margin, area.Right - requested.Width - margin));
            int top = Math.Min(
                Math.Max(area.Top + margin, requested.Top),
                Math.Max(area.Top + margin, area.Bottom - requested.Height - margin));
            return new Rectangle(left, top, requested.Width, requested.Height);
        }

        private static Screen GetForegroundScreen()
        {
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            return foreground == IntPtr.Zero
                ? Screen.FromPoint(Cursor.Position)
                : Screen.FromHandle(foreground);
        }

        private void OpeningTimerTick(object sender, EventArgs eventArgs)
        {
            double progress = Math.Min(
                1D,
                openingWatch.Elapsed.TotalMilliseconds / OpeningDurationMilliseconds);
            double targetOpacity = GetEffectiveOpacity();
            Opacity = targetOpacity * (1D - Math.Pow(1D - progress, 3D));
            if (progress >= 1D)
            {
                openingTimer.Stop();
                openingWatch.Stop();
                Opacity = targetOpacity;
            }
        }

        private void SaltoVisibleChanged(object sender, EventArgs eventArgs)
        {
            if (!Visible)
            {
                openingTimer.Stop();
                openingWatch.Reset();
                presentationTimer.Stop();
                presentationWatch.Reset();
                loadingTimer.Stop();
                Opacity = GetEffectiveOpacity();
            }
            else
            {
                UpdateLoadingPresentation();
            }
        }

        private void PositionOnForegroundScreen()
        {
            Screen screen = GetForegroundScreen();
            Rectangle area = screen.WorkingArea;
            Point savedAnchor = new Point(settings.SaltoCenterX, settings.SaltoTopY);
            if (settings.SaltoCenterX >= 0
                && settings.SaltoTopY >= 0
                && area.Contains(savedAnchor))
            {
                Bounds = ClampWindowBounds(
                    new Rectangle(
                        savedAnchor.X - (Width / 2),
                        savedAnchor.Y,
                        Width,
                        Height),
                    area);
                return;
            }

            CenterOnForegroundScreen(false);
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

    internal sealed class SaltoOpacityButton : Control
    {
        private ThemePalette palette;
        private bool hovered;
        private bool pressed;
        private int opacityPercent = 100;

        public SaltoOpacityButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw
                    | ControlStyles.SupportsTransparentBackColor
                    | ControlStyles.UserPaint,
                true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            Invalidate();
        }

        public void SetOpacityPercent(int percent)
        {
            opacityPercent = percent;
            AccessibleDescription = "Opacidad actual: " + percent + " por ciento";
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs eventArgs)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(eventArgs);
        }

        protected override void OnMouseLeave(EventArgs eventArgs)
        {
            hovered = false;
            pressed = false;
            Invalidate();
            base.OnMouseLeave(eventArgs);
        }

        protected override void OnMouseDown(MouseEventArgs eventArgs)
        {
            if (eventArgs.Button == MouseButtons.Left)
            {
                pressed = true;
                Invalidate();
            }

            base.OnMouseDown(eventArgs);
        }

        protected override void OnMouseUp(MouseEventArgs eventArgs)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(eventArgs);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            ThemePalette colors = palette ?? ThemeService.Current();
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (hovered || pressed)
            {
                Color surface = pressed ? colors.Surface : colors.SurfaceRaised;
                using (GraphicsPath background = DrawingPaths.RoundedRectangle(
                    new Rectangle(1, 1, Width - 2, Height - 2),
                    Math.Max(7, Height / 4)))
                using (SolidBrush fill = new SolidBrush(surface))
                {
                    eventArgs.Graphics.FillPath(fill, background);
                }
            }

            float scale = Math.Max(0.75F, Math.Min(2F, Height / 32F));
            RectangleF eye = new RectangleF(
                (Width - (18F * scale)) / 2F,
                7F * scale,
                18F * scale,
                12F * scale);
            Color glyph = Enabled ? colors.TextMuted : colors.TextFaint;
            using (GraphicsPath eyePath = new GraphicsPath())
            using (Pen pen = new Pen(glyph, Math.Max(1.4F, 1.5F * scale)))
            using (SolidBrush pupil = new SolidBrush(glyph))
            {
                eyePath.AddBezier(
                    eye.Left,
                    eye.Top + (eye.Height / 2F),
                    eye.Left + (eye.Width * 0.28F),
                    eye.Top - (eye.Height * 0.18F),
                    eye.Right - (eye.Width * 0.28F),
                    eye.Top - (eye.Height * 0.18F),
                    eye.Right,
                    eye.Top + (eye.Height / 2F));
                eyePath.AddBezier(
                    eye.Right,
                    eye.Top + (eye.Height / 2F),
                    eye.Right - (eye.Width * 0.28F),
                    eye.Bottom + (eye.Height * 0.18F),
                    eye.Left + (eye.Width * 0.28F),
                    eye.Bottom + (eye.Height * 0.18F),
                    eye.Left,
                    eye.Top + (eye.Height / 2F));
                eyePath.CloseFigure();
                eventArgs.Graphics.DrawPath(pen, eyePath);
                float pupilSize = 4F * scale;
                eventArgs.Graphics.FillEllipse(
                    pupil,
                    eye.Left + ((eye.Width - pupilSize) / 2F),
                    eye.Top + ((eye.Height - pupilSize) / 2F),
                    pupilSize,
                    pupilSize);
            }

            int levelCount = opacityPercent >= 100 ? 3 : opacityPercent >= 82 ? 2 : 1;
            float levelWidth = 4F * scale;
            float gap = 2F * scale;
            float totalWidth = (levelWidth * 3F) + (gap * 2F);
            float levelLeft = (Width - totalWidth) / 2F;
            using (SolidBrush levelBrush = new SolidBrush(colors.Primary))
            using (SolidBrush inactiveBrush = new SolidBrush(colors.Border))
            {
                for (int index = 0; index < 3; index++)
                {
                    RectangleF level = new RectangleF(
                        levelLeft + (index * (levelWidth + gap)),
                        Height - (6F * scale),
                        levelWidth,
                        Math.Max(1F, 1.7F * scale));
                    eventArgs.Graphics.FillRectangle(
                        index < levelCount ? levelBrush : inactiveBrush,
                        level);
                }
            }
        }
    }

    internal sealed class SaltoDragHandle : Control
    {
        private ThemePalette palette;
        private bool hovered;

        public SaltoDragHandle()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw
                    | ControlStyles.SupportsTransparentBackColor
                    | ControlStyles.UserPaint,
                true);
            BackColor = Color.Transparent;
            Cursor = Cursors.SizeAll;
            TabStop = false;
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs eventArgs)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(eventArgs);
        }

        protected override void OnMouseLeave(EventArgs eventArgs)
        {
            hovered = false;
            Invalidate();
            base.OnMouseLeave(eventArgs);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            ThemePalette colors = palette ?? ThemeService.Current();
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = Math.Max(0.75F, Height / 22F);
            int width = Math.Max(22, (int)Math.Round(30F * scale));
            int height = Math.Max(3, (int)Math.Round(3F * scale));
            Rectangle pill = new Rectangle(
                (ClientSize.Width - width) / 2,
                (ClientSize.Height - height) / 2,
                width,
                height);
            using (GraphicsPath path = DrawingPaths.RoundedRectangle(pill, height))
            using (SolidBrush brush = new SolidBrush(
                hovered ? colors.TextMuted : colors.TextFaint))
            {
                eventArgs.Graphics.FillPath(brush, path);
            }
        }
    }

    internal sealed class SaltoScrollIndicator : Control
    {
        private ThemePalette palette;
        private int itemCount;
        private int visibleRows;
        private int topIndex;

        public SaltoScrollIndicator()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw
                    | ControlStyles.SupportsTransparentBackColor
                    | ControlStyles.UserPaint,
                true);
            BackColor = Color.Transparent;
            TabStop = false;
            Visible = false;
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            Invalidate();
        }

        public void SetState(int totalItems, int rows, int firstVisibleIndex)
        {
            itemCount = Math.Max(0, totalItems);
            visibleRows = Math.Max(1, rows);
            topIndex = Math.Max(0, firstVisibleIndex);
            Visible = itemCount > visibleRows;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            if (itemCount <= visibleRows || Height <= 0)
            {
                return;
            }

            ThemePalette colors = palette ?? ThemeService.Current();
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int thumbHeight = Math.Max(
                Math.Min(Height, 18),
                (int)Math.Round(Height * (visibleRows / (double)itemCount)));
            int range = Math.Max(1, itemCount - visibleRows);
            int top = (int)Math.Round((Height - thumbHeight) * (topIndex / (double)range));
            Rectangle thumb = new Rectangle(0, top, Width, thumbHeight);
            using (GraphicsPath path = DrawingPaths.RoundedRectangle(
                thumb,
                Math.Max(1, Width)))
            using (SolidBrush brush = new SolidBrush(colors.TextFaint))
            {
                eventArgs.Graphics.FillPath(brush, path);
            }
        }
    }

    internal sealed class SaltoResultList : ListBox
    {
        private readonly Font titleFont;
        private readonly Font descriptionFont;
        private readonly Font shortcutFont;
        private ThemePalette palette;

        public event EventHandler ViewportChanged;

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

        public void HideNativeScrollBar()
        {
            if (IsHandleCreated)
            {
                NativeMethods.ShowScrollBar(Handle, 1, false);
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int verticalScrollStyle = 0x00200000;
                CreateParams parameters = base.CreateParams;
                parameters.Style &= ~verticalScrollStyle;
                return parameters;
            }
        }

        protected override void OnHandleCreated(EventArgs eventArgs)
        {
            base.OnHandleCreated(eventArgs);
            HideNativeScrollBar();
        }

        protected override void OnSizeChanged(EventArgs eventArgs)
        {
            base.OnSizeChanged(eventArgs);
            HideNativeScrollBar();
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

        protected override void OnMouseWheel(MouseEventArgs eventArgs)
        {
            base.OnMouseWheel(eventArgs);
            HideNativeScrollBar();
            NotifyViewportChanged();
        }

        protected override void OnKeyDown(KeyEventArgs eventArgs)
        {
            base.OnKeyDown(eventArgs);
            HideNativeScrollBar();
            NotifyViewportChanged();
        }

        protected override void OnSelectedIndexChanged(EventArgs eventArgs)
        {
            base.OnSelectedIndexChanged(eventArgs);
            HideNativeScrollBar();
            NotifyViewportChanged();
        }

        private void NotifyViewportChanged()
        {
            EventHandler handler = ViewportChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
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

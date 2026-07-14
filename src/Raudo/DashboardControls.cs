using System;
using System.Drawing;
using System.Windows.Forms;

namespace Raudo
{
    internal sealed class PulseSurface : RoundedPanel
    {
        private readonly StatusPill statusIndicator;
        private readonly Label descriptionLabel;
        private readonly Label durationLabel;
        private readonly DurationPicker durationSelector;
        private readonly RoundedButton toggleButton;
        private readonly Label detailLabel;
        private ThemePalette palette;

        public PulseSurface()
        {
            Radius = 16;
            BorderColor = Color.Transparent;
            Size = new Size(472, 176);

            statusIndicator = new StatusPill();
            statusIndicator.Font = new Font(
                "Segoe UI Semibold",
                9F,
                FontStyle.Bold,
                GraphicsUnit.Point);
            statusIndicator.Location = new Point(20, 12);
            statusIndicator.Size = new Size(112, 28);
            statusIndicator.AccessibleName = "Estado de Pulso";
            Controls.Add(statusIndicator);

            descriptionLabel = CreateLabel(
                "Mantén disponible el equipo durante una ausencia breve.",
                9.25F,
                FontStyle.Regular,
                new Point(20, 43),
                new Size(424, 22));
            Controls.Add(descriptionLabel);

            durationLabel = CreateLabel(
                "DURACIÓN",
                7.75F,
                FontStyle.Bold,
                new Point(20, 70),
                new Size(120, 20));
            Controls.Add(durationLabel);

            durationSelector = new DurationPicker();
            durationSelector.Font = new Font(
                "Segoe UI Semibold",
                9.25F,
                FontStyle.Bold,
                GraphicsUnit.Point);
            durationSelector.Location = new Point(20, 92);
            durationSelector.Size = new Size(248, 42);
            durationSelector.TabIndex = 0;
            durationSelector.AccessibleName = "Duración de Pulso";
            durationSelector.SelectionChanged += delegate
            {
                EventHandler handler = DurationChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            };
            Controls.Add(durationSelector);

            toggleButton = new RoundedButton();
            toggleButton.Font = new Font(
                "Segoe UI Semibold",
                9.5F,
                FontStyle.Bold,
                GraphicsUnit.Point);
            toggleButton.Location = new Point(280, 92);
            toggleButton.Size = new Size(172, 42);
            toggleButton.TabIndex = 1;
            toggleButton.Radius = 10;
            toggleButton.Glyph = ButtonGlyph.Play;
            toggleButton.AccessibleName = "Iniciar Pulso";
            toggleButton.Click += delegate
            {
                EventHandler handler = ToggleRequested;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            };
            Controls.Add(toggleButton);

            detailLabel = CreateLabel(
                "Sin actividad en segundo plano",
                8.25F,
                FontStyle.Regular,
                new Point(20, 145),
                new Size(432, 20));
            Controls.Add(detailLabel);
        }

        public event EventHandler DurationChanged;
        public event EventHandler ToggleRequested;

        public int SelectedMinutes
        {
            get { return durationSelector.SelectedMinutes; }
        }

        public void SetSelectedDuration(int minutes, bool notify)
        {
            durationSelector.SetSelected(minutes, notify);
        }

        public void ApplyTheme(ThemePalette currentPalette)
        {
            palette = currentPalette;
            BackColor = palette.Surface;
            BorderColor = Color.Transparent;
            descriptionLabel.ForeColor = palette.TextMuted;
            durationLabel.ForeColor = palette.TextFaint;
            detailLabel.ForeColor = palette.TextFaint;
            durationSelector.ForeColor = palette.Text;
            durationSelector.ApplyTheme(palette);
            statusIndicator.SetState(false, palette);
            Refresh();
        }

        public void SetState(
            bool active,
            int selectedMinutes,
            TimeSpan remaining,
            int pulseCount,
            string inactiveStatus)
        {
            durationSelector.Enabled = !active;
            toggleButton.Text = active ? "Detener" : "Iniciar";
            toggleButton.Glyph = active ? ButtonGlyph.Stop : ButtonGlyph.Play;
            toggleButton.AccessibleName = active
                ? "Detener Pulso"
                : "Iniciar Pulso por "
                    + DurationOption.GetLabel(selectedMinutes).ToLowerInvariant();
            toggleButton.ForeColor = active ? palette.Danger : palette.PrimaryForeground;
            toggleButton.NormalColor = active ? palette.SurfaceRaised : palette.Primary;
            toggleButton.HoverColor = active ? palette.Border : palette.PrimaryHover;
            toggleButton.FocusColor = active ? palette.Danger : palette.PrimaryForeground;
            statusIndicator.SetState(active, palette);

            if (active)
            {
                detailLabel.Text = "Restante " + FormatClock(remaining)
                    + (pulseCount == 0
                        ? "  ·  Actúa después de 45 s sin entrada"
                        : string.Format("  ·  Entradas mínimas: {0}", pulseCount));
                detailLabel.ForeColor = palette.TextMuted;
            }
            else
            {
                bool isReady = string.Equals(
                    inactiveStatus,
                    "Pulso listo",
                    StringComparison.Ordinal);
                detailLabel.Text = string.IsNullOrWhiteSpace(inactiveStatus) || isReady
                    ? "Sin actividad en segundo plano"
                    : inactiveStatus + "  ·  Sin actividad en segundo plano";
                detailLabel.ForeColor = palette.TextFaint;
            }
        }

        private static string FormatClock(TimeSpan remaining)
        {
            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }

            return string.Format(
                "{0:00}:{1:00}:{2:00}",
                (int)remaining.TotalHours,
                remaining.Minutes,
                remaining.Seconds);
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
    }

    internal sealed class ScreenCaptureSurface : RoundedPanel
    {
        private readonly CaptureGlyph glyph;
        private readonly Label titleLabel;
        private readonly Label descriptionLabel;
        private readonly RoundedButton actionButton;

        public ScreenCaptureSurface()
        {
            Radius = 14;
            BorderColor = Color.Transparent;
            Size = new Size(472, 76);

            glyph = new CaptureGlyph();
            glyph.Location = new Point(16, 16);
            Controls.Add(glyph);

            titleLabel = CreateLabel(
                "Recortar pantalla",
                9.75F,
                FontStyle.Bold,
                new Point(76, 11),
                new Size(246, 25));
            Controls.Add(titleLabel);

            descriptionLabel = CreateLabel(
                "Selecciona una región de la pantalla",
                8.25F,
                FontStyle.Regular,
                new Point(76, 36),
                new Size(270, 22));
            Controls.Add(descriptionLabel);

            actionButton = new RoundedButton();
            actionButton.Text = "Abrir";
            actionButton.Font = new Font(
                "Segoe UI Semibold",
                8.5F,
                FontStyle.Bold,
                GraphicsUnit.Point);
            actionButton.Location = new Point(376, 22);
            actionButton.Size = new Size(76, 32);
            actionButton.TabIndex = 0;
            actionButton.Radius = 9;
            actionButton.AccessibleName = "Recortar pantalla";
            actionButton.Click += delegate
            {
                EventHandler handler = ActionRequested;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            };
            Controls.Add(actionButton);
        }

        public event EventHandler ActionRequested;

        public void ApplyTheme(ThemePalette palette)
        {
            BackColor = palette.Surface;
            BorderColor = Color.Transparent;
            titleLabel.ForeColor = palette.Text;
            descriptionLabel.ForeColor = palette.TextMuted;
            glyph.ApplyTheme(palette);
            actionButton.ForeColor = palette.Text;
            actionButton.NormalColor = palette.SurfaceRaised;
            actionButton.HoverColor = palette.Border;
            actionButton.FocusColor = palette.Primary;
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
    }

    internal sealed class PreferencesSurface : RoundedPanel
    {
        private readonly Label miniTitleLabel;
        private readonly Label miniDescriptionLabel;
        private readonly ToggleSwitch miniToggle;
        private readonly Panel divider;
        private readonly Label startupTitleLabel;
        private readonly Label startupDescriptionLabel;
        private readonly ToggleSwitch startupToggle;

        public PreferencesSurface()
        {
            Radius = 14;
            BorderColor = Color.Transparent;
            Size = new Size(472, 124);

            miniTitleLabel = CreateLabel(
                "Modo Mini",
                9.5F,
                FontStyle.Bold,
                new Point(20, 8),
                new Size(310, 24));
            Controls.Add(miniTitleLabel);

            miniDescriptionLabel = CreateLabel(
                "Navega entre escritorios y trae ventanas",
                8.25F,
                FontStyle.Regular,
                new Point(20, 31),
                new Size(350, 22));
            Controls.Add(miniDescriptionLabel);

            miniToggle = new ToggleSwitch();
            miniToggle.Location = new Point(406, 17);
            miniToggle.TabIndex = 0;
            miniToggle.AccessibleName = "Activar Modo Mini";
            miniToggle.CheckedChanged += delegate
            {
                EventHandler handler = MiniModeChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            };
            Controls.Add(miniToggle);

            divider = new Panel();
            divider.Location = new Point(20, 61);
            divider.Size = new Size(432, 1);
            Controls.Add(divider);

            startupTitleLabel = CreateLabel(
                "Iniciar con Windows",
                9.5F,
                FontStyle.Bold,
                new Point(20, 69),
                new Size(310, 24));
            Controls.Add(startupTitleLabel);

            startupDescriptionLabel = CreateLabel(
                "Disponible en la bandeja al iniciar sesión",
                8.25F,
                FontStyle.Regular,
                new Point(20, 92),
                new Size(350, 22));
            Controls.Add(startupDescriptionLabel);

            startupToggle = new ToggleSwitch();
            startupToggle.Location = new Point(406, 78);
            startupToggle.TabIndex = 1;
            startupToggle.AccessibleName = "Iniciar Raudo con Windows";
            startupToggle.CheckedChanged += delegate
            {
                EventHandler handler = StartupChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            };
            Controls.Add(startupToggle);
        }

        public event EventHandler MiniModeChanged;
        public event EventHandler StartupChanged;

        public bool MiniModeEnabled
        {
            get { return miniToggle.Checked; }
            set { miniToggle.Checked = value; }
        }

        public bool StartupEnabled
        {
            get { return startupToggle.Checked; }
            set { startupToggle.Checked = value; }
        }

        public void ApplyTheme(ThemePalette palette)
        {
            BackColor = palette.Surface;
            BorderColor = Color.Transparent;
            miniTitleLabel.ForeColor = palette.Text;
            startupTitleLabel.ForeColor = palette.Text;
            miniDescriptionLabel.ForeColor = palette.TextMuted;
            startupDescriptionLabel.ForeColor = palette.TextMuted;
            divider.BackColor = palette.Border;
            miniToggle.ApplyTheme(palette);
            startupToggle.ApplyTheme(palette);
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
    }
}

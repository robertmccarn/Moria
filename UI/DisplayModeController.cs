using System.Drawing;
using System.Windows.Forms;

namespace Moria;

public sealed partial class Game
{
    private DisplayModeController? displayModeController;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        displayModeController ??= new DisplayModeController(this);
    }

    private sealed class DisplayModeController
    {
        private enum DisplayMode
        {
            Windowed,
            Borderless,
            Fullscreen
        }

        private readonly Game form;
        private readonly Rectangle windowedBounds;
        private DisplayMode mode;

        public DisplayModeController(Game owner)
        {
            form = owner;
            windowedBounds = owner.Bounds;
            mode = DisplayMode.Windowed;
            owner.KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.F11 || e.Alt || e.Control || e.Shift) return;
            CycleMode();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void CycleMode()
        {
            mode = mode switch
            {
                DisplayMode.Windowed => DisplayMode.Borderless,
                DisplayMode.Borderless => DisplayMode.Fullscreen,
                _ => DisplayMode.Windowed
            };

            switch (mode)
            {
                case DisplayMode.Windowed:
                    SetWindowed();
                    break;
                case DisplayMode.Borderless:
                    SetBorderless();
                    break;
                case DisplayMode.Fullscreen:
                    SetFullscreen();
                    break;
            }
        }

        private void SetWindowed()
        {
            form.TopMost = false;
            form.FormBorderStyle = FormBorderStyle.FixedSingle;
            form.MaximizeBox = false;
            form.WindowState = FormWindowState.Normal;
            form.Bounds = windowedBounds;
        }

        private void SetBorderless()
        {
            form.TopMost = false;
            form.FormBorderStyle = FormBorderStyle.None;
            form.MaximizeBox = false;
            form.WindowState = FormWindowState.Maximized;
        }

        private void SetFullscreen()
        {
            Screen screen = Screen.FromControl(form);
            form.TopMost = true;
            form.FormBorderStyle = FormBorderStyle.None;
            form.WindowState = FormWindowState.Normal;
            form.Bounds = screen.Bounds;
        }
    }
}

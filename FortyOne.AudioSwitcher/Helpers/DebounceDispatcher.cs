using System;
using System.Windows.Forms;

namespace FortyOne.AudioSwitcher.Helpers
{
    /// <summary>
    /// Coalesces rapid UI work onto a single delayed callback (WinForms timer).
    /// </summary>
    public sealed class DebounceDispatcher : IDisposable
    {
        private readonly Timer _timer;
        private Action _pending;

        public DebounceDispatcher(int delayMs)
        {
            _timer = new Timer { Interval = Math.Max(1, delayMs) };
            _timer.Tick += TimerOnTick;
        }

        public void Debounce(Action action)
        {
            if (action == null)
                return;

            _pending = action;
            _timer.Stop();
            _timer.Start();
        }

        public void Flush()
        {
            if (_pending == null)
                return;

            _timer.Stop();
            var action = _pending;
            _pending = null;
            action();
        }

        private void TimerOnTick(object sender, EventArgs e)
        {
            _timer.Stop();
            var action = _pending;
            _pending = null;
            if (action != null)
                action();
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= TimerOnTick;
            _timer.Dispose();
            _pending = null;
        }
    }
}

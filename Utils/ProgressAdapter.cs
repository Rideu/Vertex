using System;

using Android.Widget;

namespace Vertex.Utils
{
    class ProgressAdapter : IProgress<double>
    {
        private readonly ProgressBar progbar;

        public ProgressAdapter(ProgressBar pb)
        {
            progbar = pb;
        }

        public void Report(double value)
        {
            var v = (int)(value * 100);
            progbar.SetProgress(v, false);
            if (v >= 100)
                OnDone?.Invoke(null, EventArgs.Empty);
        }

        public event EventHandler OnDone;
    }
}

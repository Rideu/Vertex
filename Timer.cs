using System;
using System.Threading;
using System.Threading.Tasks;

namespace Vertex
{
    public class Timer
    {
        private readonly Action action;
        private readonly int period;
        private readonly CancellationTokenSource cancelTimerTask;
        private Task t;
        private bool running;

        public Timer(Action action, int period)
        {
            this.action = action;
            this.period = period;
            cancelTimerTask = new CancellationTokenSource();
        }

        public void Start()
        {
            running = true;
            t = Task.Run(async () =>
            {
                while (!cancelTimerTask.IsCancellationRequested)
                {
                    action();
                    await Task.Delay(period);
                }
            }, cancelTimerTask.Token);
        }

        public void Stop()
        {
            cancelTimerTask.Cancel();
        }
    }
}

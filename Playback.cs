using System;
using System.Threading;
using System.Threading.Tasks;
using Android.Media;
using Android.Content;

namespace Vertex
{
    public class Loop
    {
        Task t;
        Action action;

        public Loop(Action a)
        {
            action = a;
        }

        public void Start()
        {
            running = true;
            t = new Task(LoopExec);
            t.Start();
        }

        bool running;

        void LoopExec()
        {
            while (running)
            {
                action();
            }
        }

        public void Stop()
        {
            running = false;
            //t.Dispose();
        }
    }

    public partial class ContentActivity
    {
        class Playback
        {
            MediaPlayer mp;
            Context ctx;
            Loop timespanTask;
            AudioManager audioManager;
            int headsetWarn;
            //bool running;
            public Playback(Context ctx)
            {
                this.ctx = ctx;
                audioManager = (AudioManager)ctx.GetSystemService(AudioService);
                //timespanTask = new Task(() => TrackTimestamp());
            }

            public TimeSpan CurrentPosition
            {
                get => TimeSpan.FromMilliseconds((int)(mp?.CurrentPosition ?? 0));
                set { SeekPrecise((int)value.TotalSeconds); }
            }
            public TimeSpan Duration { get => TimeSpan.FromMilliseconds((int)(mp?.Duration ?? 0)); }

            public float Progress => (float)(CurrentPosition.TotalSeconds / Duration.TotalSeconds);

            public event EventHandler ProgressChanged;

            void PlaybackUpdate()
            {
                if (IsPlaying)
                {
                    ProgressChanged?.BeginInvoke(null, EventArgs.Empty, null, null);

                    if (!audioManager.WiredHeadsetOn)
                    {
                        headsetWarn++;
                        if (headsetWarn == 4)
                        {
                            Pause();
                            OnPaused?.Invoke(this, EventArgs.Empty);
                        }
                    }
                    else
                    {
                        headsetWarn = 0;
                    }
                }
                Thread.Sleep(500);
            }

            public bool IsPlaying => mp?.IsPlaying ?? false;

            Java.IO.File audioFile;
            public void SetAudio(string path)
            {
                mp?.Stop();
                mp?.Dispose();

                audioFile = new Java.IO.File(path);
                mp = MediaPlayer.Create(ctx, Android.Net.Uri.FromFile(audioFile));
                mp.Looping = true;
                //mp.SetDataSource(path);
                mp.BufferingUpdate += (s, e) =>
                {

                };

                mp.SeekComplete += (s, e) =>
                {

                };

                //mp.PlaybackParams.
                mp.Completion += (s, e) =>
                {
                    //running = false;
                    OnFinished?.Invoke(s, e);
                };
            }

            public void Play()
            {
                if (CanPlay)
                {
                    mp.Start();

                    timespanTask?.Stop();

                    //running = true;
                    timespanTask = new Loop(PlaybackUpdate);
                    timespanTask.Start();
                }
            }

            public event EventHandler OnPaused;

            public void Pause() { if (CanPlay) { mp.Pause(); } }

            public bool CanPlay => mp != null;

            public void Seek(float f) => mp?.SeekTo((long)(mp.Duration * f), MediaPlayerSeekMode.NextSync);

            public void SeekPrecise(long msec) => mp?.SeekTo(msec, MediaPlayerSeekMode.NextSync);

            public event EventHandler OnFinished;
        }
    }
}

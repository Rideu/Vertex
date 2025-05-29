using System;
using Android.App;
using Android.Preferences;
using Android.Media;
using Android.Content;
using Android.Media.Audiofx;
using Vertex.Utils;
using Vertex.Interfaces;
using Android.Bluetooth;

using Android.Util;

namespace Vertex.Media
{
    public class Playback : IAudioOutputStateHandler
    {
        private static readonly string TAG = "Playback";
        private readonly Activity activity;
        private readonly AudioManager audioManager;
        private MediaPlayer mediaPlayer;
        private Timer timespanTask; 
        private Java.IO.File audioFile;
        private Equalizer equalizer;
        private ISharedPreferences sharedPreferences;

        public TimeSpan CurrentPosition
        {
            get => TimeSpan.FromMilliseconds(mediaPlayer?.CurrentPosition ?? 0);
            set { SeekPrecise((int)value.TotalSeconds); }
        }

        public bool IsPlaying => mediaPlayer?.IsPlaying ?? false;

        public TimeSpan Duration { get => TimeSpan.FromMilliseconds(mediaPlayer?.Duration ?? 0); }

        public float Progress => (float)(CurrentPosition.TotalSeconds / Duration.TotalSeconds);

        public short[] EqualizerBandLevels { get; private set; } = { 0, 0, 0, 0, 0 };

        public bool CanPlay => mediaPlayer != null;

        public bool IsInterrupted { get; private set; }

        public event EventHandler ProgressChanged;
        public event EventHandler OnPaused;
        public event EventHandler OnResume;
        public event EventHandler OnFinished;

        public Playback(Activity ctx)
        {
            activity = ctx;
            audioManager = (AudioManager)ctx.GetSystemService(Context.AudioService);

            LoadPreferences();
        }

        private void LoadPreferences()
        {
            sharedPreferences = PreferenceManager.GetDefaultSharedPreferences(activity);

            var joinBands = sharedPreferences.GetString("EqualizerBandLevels", null);

            if (joinBands != null)
            {
                var split = joinBands.Split(',');

                for (int i = 0; i < EqualizerBandLevels.Length; i++)
                    EqualizerBandLevels[i] = short.Parse(split[i]);
            }

        }

        private void BackgroundTick()
        {
            if (IsPlaying)
                activity.RunOnUiThread(InvokeProgressChanged);
        }

        private void SetupEqualizer()
        {

            equalizer?.Release();

            equalizer = new Equalizer(0, mediaPlayer.AudioSessionId);
            equalizer.SetEnabled(true);

            short bands = equalizer.NumberOfBands;
            var bandlevelranges = equalizer.GetBandLevelRange();

            short minlevel = bandlevelranges[0];
            short maxlevel = bandlevelranges[1];

            equalizer.SetBandLevel(0, maxlevel);
        }

        private static bool IsEqualizerSupported()
        {
            bool isSupported = false;
            AudioEffect.Descriptor[] descriptors = AudioEffect.QueryEffects();

            foreach (AudioEffect.Descriptor descriptor in descriptors)
                if (descriptor.Type.Equals(AudioEffect.EffectTypeEqualizer))
                {
                    isSupported = true;
                    break;
                }

            return isSupported;
        }

        internal void SaveEqualizerBands()
        {
            ISharedPreferencesEditor editor = sharedPreferences.Edit();

            var joinBands = string.Join(",", EqualizerBandLevels);
            editor.PutString("EqualizerBandLevels", joinBands);

            editor.Apply();
        }

        public void SetAudio(string path)
        {
            mediaPlayer?.Stop();
            mediaPlayer?.Dispose();

            audioFile = new Java.IO.File(path);
            var furi = Android.Net.Uri.FromFile(audioFile);

            mediaPlayer = new MediaPlayer();
            mediaPlayer.SetDataSource(activity, furi);
            mediaPlayer.Prepare();

            var iseqsupported = IsEqualizerSupported();

            if (iseqsupported)
            {

                try
                {
                    SetupEqualizer();
                }
                catch (Exception e)
                {
                    Log.Warn(TAG, $"Couldn't init equalizer: {e.Message}");
                }
            }


            mediaPlayer.Looping = true;

            mediaPlayer.BufferingUpdate += (s, e) =>
            {

            };

            mediaPlayer.SeekComplete += (s, e) =>
            {

            };

            mediaPlayer.Completion += (s, e) =>
            {
                OnFinished?.Invoke(s, e);
            };
        }

        public void Play()
        {
            if (CanPlay)
            {
                mediaPlayer.Start();

                timespanTask?.Stop();

                timespanTask = new Timer(BackgroundTick, 500);
                timespanTask.Start();

                if (IsInterrupted)
                    activity.RunOnUiThread(InvokeOnResume);
            }
        }

        private void InvokeProgressChanged()
        {
            ProgressChanged?.Invoke(null, EventArgs.Empty);
        }

        private void InvokeOnResume()
        {
            OnResume?.Invoke(this, EventArgs.Empty);
        }

        private void InvokeOnPaused()
        {
            OnPaused?.Invoke(this, EventArgs.Empty);
        }

        public void Pause()
        {
            if (CanPlay)
            {
                mediaPlayer?.Pause();
                activity.RunOnUiThread(InvokeOnPaused);
            }
        }


        public void Seek(float f) => mediaPlayer?.SeekTo((long)(mediaPlayer.Duration * f), MediaPlayerSeekMode.NextSync);

        public void SeekPrecise(long msec) => mediaPlayer?.SeekTo(msec, MediaPlayerSeekMode.NextSync);

        public void ChangeBand(short band, short value)
        {
            lock (EqualizerBandLevels)
            {
                EqualizerBandLevels[band] = value;
                equalizer?.SetBandLevel(band, value);
            }
        }

        internal void Release()
        {
            equalizer?.Release();
            mediaPlayer?.Release();
            timespanTask?.Release();
        }

        void IAudioOutputStateHandler.OnStateChanged(AudioStateEventArgs e)
        {
            switch (e.AudioOutputState)
            {
                case AudioOutputState.Connected:

                    if (IsInterrupted)
                    {
                        if (e.Device.DeviceClass == DeviceClass.AudioVideoWearableHeadset)
                            Play();

                        IsInterrupted = false;
                    }

                    break;
                case AudioOutputState.Disconnected:

                    if (e.Device.DeviceClass == DeviceClass.AudioVideoWearableHeadset)
                    { 
                        if (IsPlaying)
                            IsInterrupted = true;

                        Pause();
                    }

                    break;
                default:
                    break;
            }
        }
    }

}

using System;
using System.Threading;
using System.Threading.Tasks;
using Android.Media;
using Android.Content;
using Android.Media.Audiofx;
using System.Linq;
using static Android.Media.Audiofx.AudioEffect;
using Android.App;
using Android.Preferences;

namespace Vertex
{

    public partial class ContentActivity
    {
        public class Playback
        {
            private readonly Activity activity;
            private readonly AudioManager audioManager;
            private MediaPlayer mediaPlayer;
            private Timer timespanTask;
            private int headsetWarn;
            private Java.IO.File audioFile;
            private Equalizer equalizer; 
            private ISharedPreferences sharedPreferences;

            public TimeSpan CurrentPosition
            {
                get => TimeSpan.FromMilliseconds((int)(mediaPlayer?.CurrentPosition ?? 0));
                set { SeekPrecise((int)value.TotalSeconds); }
            }

            public bool IsPlaying => mediaPlayer?.IsPlaying ?? false;

            public TimeSpan Duration { get => TimeSpan.FromMilliseconds((int)(mediaPlayer?.Duration ?? 0)); }

            public float Progress => (float)(CurrentPosition.TotalSeconds / Duration.TotalSeconds);

            public short[] EqualizerBandLevels { get; private set; } = { 0, 0, 0, 0, 0 };

            public bool CanPlay => mediaPlayer != null;

            public event EventHandler ProgressChanged;
            public event EventHandler OnPaused;
            public event EventHandler OnFinished;

            public Playback(Activity ctx)
            {
                this.activity = ctx;
                audioManager = (AudioManager)ctx.GetSystemService(AudioService);

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
                    {
                        EqualizerBandLevels[i] = short.Parse(split[i]);
                    }
                }

            } 

            private void BackgroundTick()
            {
                if (IsPlaying)
                {
                    activity.RunOnUiThread(PeekPlaybackUpdate);
                } 
            }

            private void PeekPlaybackUpdate()
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
                Descriptor[] descriptors = AudioEffect.QueryEffects();

                foreach (Descriptor descriptor in descriptors)
                {
                    if (descriptor.Type.Equals(AudioEffect.EffectTypeEqualizer))
                    {
                        isSupported = true;
                        break;
                    }
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

                    //running = true;
                    timespanTask = new Timer(BackgroundTick, 500);
                    timespanTask.Start();
                }
            }

            public void Pause()
            {
                if (CanPlay)
                {
                    mediaPlayer.Pause();
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
        }
    }
}

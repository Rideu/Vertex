using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Widget;
using Android.Views;
using Android.Support.Design.Widget;
using Xamarin.Essentials;
using Android.Media;

using YoutubeExplode;

using Vertex.Media;
using Vertex.Utils;

using static Xamarin.Essentials.Permissions;

namespace Vertex
{
    [Activity(Label = "@string/app_name", Theme = "@style/VertexTheme", MainLauncher = true)]
    public partial class ContentActivity : AppCompatActivity
    {
        private const string STR_PLAY = "►";
        private const string STR_PAUSE = "II";

        private readonly List<TrackData> tracks = new List<TrackData>();

        private View contentMain;

        private ProgressBar loadingProgress;

        private Button
            buttonStart,
            buttonPlay;

        private ListView listMenu;

        private EditText uriEnterText;

        private TextView
            reporterText,
            nameholderText,
            durationText,
            textViewCurrentTime,
            textViewLength;

        private Playback mediaPlayback;

        private SeekBar seekAudio;

        private ProgressAdapter pga;
        private Button buttonEqualizer;
        private EqualizerController equalizerController;

        protected async override void OnCreate(Bundle savedInstanceState)
        {
            AppCompatDelegate.DefaultNightMode = AppCompatDelegate.ModeNightYes;

            base.OnCreate(savedInstanceState);

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);

            SetContentView(Resource.Layout.content_main);


            #region Controls

            contentMain = FindViewById<RelativeLayout>(Resource.Id.contentMain);

            loadingProgress = FindViewById<ProgressBar>(Resource.Id.progressBar1);

            nameholderText = FindViewById<TextView>(Resource.Id.textView2);
            reporterText = FindViewById<TextView>(Resource.Id.textView3);
            durationText = FindViewById<TextView>(Resource.Id.textViewTimeSpan);
            textViewCurrentTime = FindViewById<TextView>(Resource.Id.textViewCurrentTime);
            textViewLength = FindViewById<TextView>(Resource.Id.textViewLength);

            uriEnterText = FindViewById<EditText>(Resource.Id.editText1);

            listMenu = FindViewById<ListView>(Resource.Id.listMenu1);

            buttonStart = FindViewById<Button>(Resource.Id.button1);
            buttonStart.Click += InitLoader;

            buttonPlay = FindViewById<Button>(Resource.Id.buttonPlay);
            buttonPlay.Enabled = false;
            buttonPlay.Click += (s, e) =>
            {
                if (mediaPlayback.CanPlay)
                {
                    if (mediaPlayback.IsPlaying)
                    {
                        buttonPlay.Text = STR_PLAY;
                        mediaPlayback.Pause();
                    }
                    else
                    {
                        buttonPlay.Text = STR_PAUSE;
                        mediaPlayback.Play();
                    }
                }
            };

            buttonEqualizer = FindViewById<Button>(Resource.Id.buttonEqualizer);
            buttonEqualizer.Enabled = true;
            buttonEqualizer.Click += (s, e) =>
            {

                equalizerController ??= new EqualizerController(this, mediaPlayback);
                equalizerController.ShowDialog();

            };

            seekAudio = FindViewById<SeekBar>(Resource.Id.seekBarAudioSeek);
            seekAudio.ProgressChanged += (s, e) =>
            {

                if (seekAudio.Pressed)
                    mediaPlayback.Seek(seekAudio.Progress / 100f);
            };

            var img = FindViewById<ImageView>(Resource.Id.imageView1);
            img.SetImageResource(Resource.Drawable.back);

            #endregion


            if (await RequirePermissionAsync(new StorageRead()))
            {
                InitTracks();
            }

            mediaPlayback = new Playback(this);

            mediaPlayback.OnFinished += (s, e) =>
            {
                buttonPlay.Text = STR_PLAY;
            };

            mediaPlayback.ProgressChanged += (s, e) =>
            {
                seekAudio.Progress = (int)(mediaPlayback.Progress * 100);
                textViewCurrentTime.Text = $"{mediaPlayback.CurrentPosition:hh\\:mm\\:ss}";
            };

            mediaPlayback.OnPaused += (s, e) =>
            {
                buttonPlay.Text = STR_PLAY;
            };

            pga = new ProgressAdapter(loadingProgress);
            pga.OnDone += LoadingDone;
        }

        private static async Task<bool> RequirePermissionAsync(BasePlatformPermission storageRead)
        {
            var permissionStatus = await storageRead.CheckStatusAsync();

            if (permissionStatus == PermissionStatus.Denied)
            {
                permissionStatus = await storageRead.RequestAsync();
                if (permissionStatus == PermissionStatus.Granted)
                {
                    return true;
                }
                return false;
            }
            else
            {
                return true;
            }
        }

        private void InitTracks()
        {

            string path = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryMusic).ToString();

            var files = Directory.EnumerateFiles(path);
            var yts = files.Where(n =>
            n.Contains("Music/[YE]"));

            files = yts.OrderBy(n => n).Concat(files.Except(yts).OrderBy(n => n));

            MediaMetadataRetriever mtr = new MediaMetadataRetriever();

            foreach (var f in files)
            {
                try
                {
                    var tdata = TrackData.FromFile(f);
                    tracks.Add(tdata);
                }
                catch (Exception)
                {
                }
            }

            TrackDataAdapter tda = new TrackDataAdapter(this, tracks);

            tda.OnTrackPicked += (s, e) =>
            {
                SetPlayerAudio(e);
            };

            listMenu.Adapter = tda;
        }

        private void InitLoader(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(uriEnterText.Text))
            {
                var m = Regex.Match(uriEnterText.Text, @"((?<=youtube.com/watch\?v=).{11})|((?<=(//youtu.be/)).{11})");
                if (m.Success)
                {
                    try
                    {
                        uriEnterText.ClearFocus();
                        LoadAudio($"https://youtube.com/watch?v={m.Value}");
                        buttonStart.Enabled = true;
                    }
                    catch (Exception)
                    {
                        buttonStart.Enabled = true;
                        reporterText.Text = "Error occured, try again."; 
                    }
                }
                else
                    reporterText.Text = "Invalid URI. Check your link.";
            }
            else
                reporterText.Text = "Field is empty.";
        }

        private void LoadingDone(object sender, EventArgs e)
        {
            reporterText.Text = $"Loading done.";
            buttonStart.Enabled = true;
        }

        private async void LoadAudio(string uri)
        {
            buttonStart.Enabled = false;

            reporterText.Text = "Getting video metadata...";
            var youtube = new YoutubeClient { };

            try
            {
                var video = await youtube.Videos.GetAsync(uri);

                var title = video.Title;
                var duration = video.Duration;

                nameholderText.Text = $"{title}";
                durationText.Text = $"{duration:hh\\:mm\\:ss}";

                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);
                var astreams = streamManifest.GetAudioOnlyStreams();
                var audio = astreams.First();
                var proceed = true;

                if (audio != null && proceed)
                { 

                    if (!await RequirePermissionAsync(new StorageWrite()))
                    { 
                        Snackbar.Make(contentMain, $"Cannot write a file without the required permission", Snackbar.LengthLong).Show();
                        return;
                    }

                    string path = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryMusic).AbsolutePath;

                    var state = Android.OS.Environment.ExternalStorageState;
                    
                    if (state == "mounted")
                    {

                        reporterText.Text = $"Downloading...";
                        var fullpath = $"{path}/[YE] {title.Replace("/", @"\")}." + audio.Container.Name;

                        await youtube.Videos.Streams.DownloadAsync(audio, fullpath, pga);

                        SetPlayerAudio(TrackData.FromFile(fullpath));
                    }
                    else
                    {
                        Snackbar.Make(contentMain, $"Cannot write a file without the required permission", Snackbar.LengthLong).Show();
                    }
                }
            }
            catch (System.Net.Http.HttpRequestException)
            {
                Snackbar.Make(contentMain, $"Error occured. No Internet connection.", Snackbar.LengthLong).Show();
            }
            catch (System.IO.IOException e)
            {
                if (e.HResult == -2147024864)
                {
                    Snackbar.Make(contentMain, $"Error occured. Cannot write to device.", Snackbar.LengthLong).Show();
                }
                else
                    Snackbar.Make(contentMain, $"Error occured. Internet connection is down.", Snackbar.LengthLong).Show();
            }
            catch (Exception)
            {
                Snackbar.Make(contentMain, "Error occured. Try again.", Snackbar.LengthLong).Show();
            }
        }

        public void SetPlayerAudio(TrackData td)
        {
            try
            {

                mediaPlayback.SetAudio(td.Path);

                buttonPlay.Text = STR_PLAY;
                buttonPlay.Enabled = true;

                nameholderText.Text = $"{td.TrackName}";
                textViewLength.Text = $"{mediaPlayback.Duration:hh\\:mm\\:ss}";
                durationText.Text = $"{td.Duration:hh\\:mm\\:ss}";
                textViewCurrentTime.Text = $"{new TimeSpan():hh\\:mm\\:ss}";
                reporterText.Text = $"";
            }
            catch (Exception e)
            {
                Snackbar.Make(contentMain, "Failed to play audio", Snackbar.LengthLong).Show();
                //reporterText.Text = $"Failed to play audio";
                System.Diagnostics.Debug.WriteLine(e.Message);
                nameholderText.Text = $"";
                textViewLength.Text = $"";
                durationText.Text = $"{new TimeSpan():hh\\:mm\\:ss}";
                textViewCurrentTime.Text = $"{new TimeSpan():hh\\:mm\\:ss}";
            }
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}

using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

using Android.App;
using Android.Webkit;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Support.V7.App;
using Android.Support.V7.View.Menu;
using Android.Runtime;
using Android.Widget;
using Android.Net;
using Android.Gestures;
using Android.Views;
using Android.Util;
using Android.Support.Design.Widget;

using Xamarin;
using Xamarin.Essentials;
using Xamarin.Android;

using YoutubeExplode;

using YoutubeExplode.Videos;
using YoutubeExplode.Common;
using YoutubeExplode.Search;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Videos.ClosedCaptions;

using NAudio;
using NAudio.Wave.WaveFormats;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Dsp;
using NAudio.Wave.Compression;
using NAudio.Wave.Asio;
using NAudio.MediaFoundation;
using NAudio.FileFormats.Wav;
using NAudio.FileFormats.Mp3;
using Android.Media;

namespace Vertex
{
    [Activity(Label = "@string/app_name", Theme = "@style/VertexTheme", MainLauncher = true)]
    public partial class ContentActivity : AppCompatActivity
    {
        const string STR_PLAY = "►";
        const string STR_PAUSE = "II";

        ProgressBar loadingProgress;

        Button
            buttonStart,
            buttonPlay;

        ListView listMenu;

        EditText uriEnterText;

        TextView
            reporterText,
            nameholderText,
            durationText,
            textViewCurrentTime,
            textViewLength;

        Playback mediaPlayer;

        SeekBar seekAudio;

        //bool manualSeeking;

        ProgressAdapter pga;


        List<TrackData> tracks = new List<TrackData>();

        protected async override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);


            SetContentView(Resource.Layout.content_main);

            mediaPlayer = new Playback(this);
             
            //var ab = ((Activity)ApplicationContext).ActionBar; 

            #region Controls

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
                if (mediaPlayer.CanPlay)
                {
                    if (mediaPlayer.IsPlaying)
                    {
                        buttonPlay.Text = STR_PLAY;
                        mediaPlayer.Pause();
                    }
                    else
                    {
                        buttonPlay.Text = STR_PAUSE;
                        mediaPlayer.Play();
                    }
                }
            };

            Permissions.StorageWrite rs = new Permissions.StorageWrite();

            var writegtd = await rs.CheckStatusAsync();
            if (writegtd == PermissionStatus.Granted)
            {
                writegtd = await rs.RequestAsync();
                if (writegtd == PermissionStatus.Granted)
                {
                    InitTracks();
                }
            }


            seekAudio = FindViewById<SeekBar>(Resource.Id.seekBarAudioSeek);
            seekAudio.ProgressChanged += (s, e) =>
            {
                if (seekAudio.Pressed)
                    mediaPlayer.Seek(seekAudio.Progress / 100f);
            };

            var img = FindViewById<ImageView>(Resource.Id.imageView1);
            img.SetImageResource(Resource.Drawable.back);

            #endregion

            mediaPlayer.OnFinished += (s, e) =>
            {
                buttonPlay.Text = STR_PLAY;
            };

            mediaPlayer.ProgressChanged += (s, e) =>
            {
                seekAudio.Progress = (int)(mediaPlayer.Progress * 100);
                textViewCurrentTime.Text = $"{mediaPlayer.CurrentPosition:hh\\:mm\\:ss}";
            };

            pga = new ProgressAdapter(loadingProgress);
            pga.OnDone += LoadingDone; 
        }

        void InitTracks()
        {

            string path = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryMusic).ToString();

            var files = Directory.EnumerateFiles(path);
            var yts = files.Where(n =>
            n.Contains("Music/[YE]"));

            files = yts.OrderBy(n => n).Concat(files.Except(yts).OrderBy(n => n));



            MediaMetadataRetriever mtr = new MediaMetadataRetriever();

            foreach (var f in files)
            {
                tracks.Add(TrackData.FromFile(f));
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

        async void LoadAudio(string uri)
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
                var audio = streamManifest.GetAudioOnly().WithHighestBitrate();


                if (audio != null)
                {
                    Permissions.StorageWrite rs = new Permissions.StorageWrite();

                    var writegtd = await rs.CheckStatusAsync();
                    if (writegtd != PermissionStatus.Granted)
                    {
                        await rs.RequestAsync();
                    }

                    //string path = ApplicationContext.GetExternalFilesDir(Android.OS.Environment.DirectoryMusic).ToString();
                    string path = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryMusic).AbsolutePath;
                    var state = Android.OS.Environment.ExternalStorageState;
                    if (state == "mounted")
                    {

                        reporterText.Text = $"Downloading...";
                        var fullpath = $"{path}/[YE] {title.Replace("/", @"\")}.mp3";

                        await youtube.Videos.Streams.DownloadAsync(audio, fullpath, pga);

                        SetPlayerAudio(TrackData.FromFile(fullpath)); 
                    }
                }
            }
            catch (System.Net.Http.HttpRequestException)
            {
                reporterText.Text = $"Error occured. No Internet connection.";
            }
            catch (System.IO.IOException)
            {
                reporterText.Text = $"Error occured. Internet connection is down.";
            }
            catch (Exception)
            {
                reporterText.Text = $"Error occured. Try again.";
            }
        }

        public void SetPlayerAudio(TrackData td)
        {

            mediaPlayer.SetAudio(td.Path);

            buttonPlay.Text = STR_PLAY;
            buttonPlay.Enabled = true;

            nameholderText.Text = $"{td.TrackName}";
            textViewLength.Text = $"{mediaPlayer.Duration:hh\\:mm\\:ss}";
            durationText.Text = $"{td.Duration:hh\\:mm\\:ss}";
            textViewCurrentTime.Text = $"{new TimeSpan():hh\\:mm\\:ss}";
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

        private void FabOnClick(object sender, EventArgs eventArgs)
        {
            View view = (View)sender;
            Snackbar.Make(view, "Replace with your own action", Snackbar.LengthLong)
                .SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}

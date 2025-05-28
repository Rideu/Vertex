using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace Vertex
{
    internal class EqualizerController
    {
        private Context context;
        private ContentActivity.Playback playback;
        private SeekBar eqBand1;
        private SeekBar eqBand2;
        private SeekBar eqBand3;
        private SeekBar eqBand4;
        private SeekBar eqBand5;

        public EqualizerController(Context context, ContentActivity.Playback playback)
        {
            this.context = context;
            this.playback = playback;
        }

        public void ShowDialog()
        {

            var dialog = new Dialog(context);
            dialog.SetContentView(Resource.Layout.EqualizerView);

            Window window = dialog.Window;
            window.SetLayout(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent);

            var bands = new List<SeekBar>
            {
                (eqBand1 = window.FindViewById<SeekBar>(Resource.Id.seekBarEqBand1)),
                (eqBand2 = window.FindViewById<SeekBar>(Resource.Id.seekBarEqBand2)),
                (eqBand3 = window.FindViewById<SeekBar>(Resource.Id.seekBarEqBand3)),
                (eqBand4 = window.FindViewById<SeekBar>(Resource.Id.seekBarEqBand4)),
                (eqBand5 = window.FindViewById<SeekBar>(Resource.Id.seekBarEqBand5)),
            };

            for (int i = 0; i < bands.Count; i++)
            {
                SeekBar band = bands[i];
                band.Progress = playback.EqualizerBandLevels[i];
                band.ProgressChanged += BandValueChanged;
            }

            dialog.Show();

            dialog.CancelEvent += (s, e) =>
            {
                playback.SaveEqualizerBands();
            };
        }

        private void BandValueChanged(object sender, SeekBar.ProgressChangedEventArgs e)
        {
            var band = (SeekBar)sender;
            var tag = band.Tag.ToString();

            var bandnum = (tag.Last() - '0') - 1;

            playback.ChangeBand((short)bandnum, (short)band.Progress);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V7.Widget;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Dalvik;
using Android.Media;

namespace Vertex
{
    public class TrackData
    {
        public string TrackName = "";
        public string Path = "";
        public TimeSpan Duration = new TimeSpan();

        public TrackData()
        {

        }

        static MediaMetadataRetriever mtr = new MediaMetadataRetriever();

        public static TrackData FromFile(string path)
        {

            mtr.SetDataSource(path);
            string durationStr = mtr.ExtractMetadata(9);


            return new TrackData
            {
                Path = path,
                TrackName = System.IO.Path.GetFileNameWithoutExtension(path),
                Duration = TimeSpan.FromMilliseconds(int.Parse(durationStr))
            };
        }
    }

    public class TrackDataAdapter : BaseAdapter<TrackData>
    {
        List<TrackData> tracks = new List<TrackData>();
        Activity activity;

        public override TrackData this[int position] => tracks[position];

        public override int Count => tracks.Count;

        public TrackDataAdapter(Activity activity, List<TrackData> trackDatas)
        {
            tracks = trackDatas;
            this.activity = activity;

        }

        public override long GetItemId(int position)
        {
            return position;
        }

        List<TrackViewBind> bindList = new List<TrackViewBind>();

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            var item = tracks[position];
            View view = convertView;
            if (view == null) // no view to re-use, create new
                view = activity.LayoutInflater.Inflate(Resource.Layout.TrackHolderView, null);

            view.FindViewById<TextView>(Resource.Id.textViewTrackName).Text = item.TrackName;
            view.FindViewById<TextView>(Resource.Id.textViewTimeStamp).Text = $"{item.Duration:hh\\:mm\\:ss}";

            var bind = new TrackViewBind(view, item);
            bind.OnPick += TrackPick;

            bindList.Add(bind);

            return view;
        }

        public event EventHandler<TrackData> OnTrackPicked;
        private void TrackPick(object sender, TrackData e)
        {
            OnTrackPicked?.Invoke(null, e);
        }

        class TrackViewBind
        {
            public View v;
            public TrackData td;

            public TrackViewBind(View v, TrackData td)
            {
                this.v = v;
                this.td = td;
                v.Click += ViewClick;
            }

            public event EventHandler<TrackData> OnPick;
            private void ViewClick(object sender, EventArgs e)
            {
                OnPick?.Invoke(this, td);
            }
        }
    }

    public class TrackHolderView : ViewGroup
    {
        //public override int ItemCount => 1;
        string trackName;
        TimeSpan duration;

        public readonly TextView tvtrackName;
        public readonly TextView tvtimeStamp;

        public TrackHolderView(Context ctx, string trackname, TimeSpan duration) : base(ctx)
        {
            trackName = trackname;
            this.duration = duration;


            AddView(tvtrackName = new TextView(ctx));
            AddView(tvtimeStamp = new TextView(ctx));
            tvtrackName.Text = trackname;
            tvtimeStamp.Text = duration.ToString();
            //tvtimeStamp = (TextView)FindViewById(Resource.Id.textViewTimeStamp);
        }

        protected override void OnLayout(bool changed, int l, int t, int r, int b)
        {

        }
    }
}
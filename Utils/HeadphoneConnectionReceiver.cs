using System;
using System.Collections.Generic;
using System.Linq;

using Android.Bluetooth;
using Android.Content;
using Android.Util;

using Vertex.Interfaces;


namespace Vertex.Utils
{
    public class HeadphoneConnectionReceiver : BroadcastReceiver
    {
        private static readonly string TAG = "HeadphoneConnectionReceiver";

        public override void OnReceive(Context context, Intent intent)
        {
            if (intent.Action == Intent.ActionHeadsetPlug)
            {
                int state = intent.GetIntExtra("state", -1);
                switch (state)
                {
                    case 0:
                        Log.Info(TAG, "Headphones disconnected");
                        // TODO: Handle headphone disconnection
                        break;
                    case 1:
                        Log.Info(TAG, "Headphones connected");
                        // TODO: Handle headphone connection
                        break;
                    default:
                        Log.Warn(TAG, "Unknown headphone state");
                        break;
                }
            }
        }
    }

    public class BluetoothConnectionReceiver : BroadcastReceiver
    {
        private static readonly string TAG = "BluetoothConnectionReceiver";
        private IAudioOutputStateHandler notificationHandler;
        private Context context;
        private BluetoothAdapter adapter;
        private List<BondedBluetoothDevice> bondedAudioDevices;
        private BluetoothManager bluetoothManager;

        internal BluetoothConnectionReceiver(Context context, IAudioOutputStateHandler handler)
        {
            notificationHandler = handler;
            this.context = context;

            var filter = new IntentFilter();
            filter.AddAction(BluetoothAdapter.ActionConnectionStateChanged);
            filter.AddAction(BluetoothA2dp.ActionPlayingStateChanged);
            filter.AddAction(BluetoothDevice.ActionAclConnected);
            filter.AddAction(BluetoothDevice.ActionAclDisconnected);
            context.RegisterReceiver(this, filter);

            bluetoothManager = (BluetoothManager)context.GetSystemService(Context.BluetoothService);
            adapter = bluetoothManager.Adapter;

            bondedAudioDevices = GetBondedAudioDevices(adapter);
        }

        private List<BondedBluetoothDevice> GetBondedAudioDevices(BluetoothAdapter adapter)
        {
            List<BondedBluetoothDevice> connectedDevices = new List<BondedBluetoothDevice>();
            if (adapter == null || !adapter.IsEnabled)
            {
                return connectedDevices;
            }

            var devices = adapter.BondedDevices;
            if (devices != null)
            {
                foreach (var device in devices)
                {
                    if (device.BluetoothClass.DeviceClass == DeviceClass.AudioVideoWearableHeadset)
                        connectedDevices.Add(new BondedBluetoothDevice(device));
                }
            }

            return connectedDevices;
        }

        public override void OnReceive(Context context, Intent intent)
        {
            string action = intent.Action;

            if (action == BluetoothAdapter.ActionConnectionStateChanged)
            {
                int state = intent.GetIntExtra(BluetoothAdapter.ExtraConnectionState, BluetoothAdapter.Error);

                switch (state)
                {
                    case 1:
                        Log.Info(TAG, "Bluetooth device connecting");
                        notificationHandler?.OnStateChanged(new AudioStateEventArgs(AudioOutputState.Connecting, null));
                        break;
                    //case 2:
                    //    Log.Info(TAG, "Bluetooth device connected");
                    //    notificationHandler?.OnStateChanged(AudioOutputState.Connected);
                    //    break;
                    //case 0:
                    //    Log.Info(TAG, "Bluetooth device disconnected");
                    //    notificationHandler?.OnStateChanged(new AudioStateEventArgs(AudioOutputState.Disconnected, null));
                    //    break;
                    default:
                        Log.Warn(TAG, $"Unknown Bluetooth state: {state}");
                        break;
                }
            }


            if (action == BluetoothDevice.ActionAclConnected)
            {
                var device = (BluetoothDevice)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);
                Log.Info(TAG, $"Bluetooth device connected: {device.Name}");

                bondedAudioDevices = GetBondedAudioDevices(adapter);

                var actualdevice = bondedAudioDevices?.FirstOrDefault(n => n.Address == device.Address);

                if (device.BluetoothClass.DeviceClass == DeviceClass.AudioVideoWearableHeadset)
                    notificationHandler?.OnStateChanged(new AudioStateEventArgs(AudioOutputState.Connected, actualdevice));
            }
            else
            if (action == BluetoothDevice.ActionAclDisconnected)
            {
                var device = (BluetoothDevice)intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);
                Log.Info(TAG, $"Bluetooth device disconnected: {device.Name}");

                var actualdevice = bondedAudioDevices?.FirstOrDefault(n => n.Address == device.Address);

                if (actualdevice.DeviceClass == DeviceClass.AudioVideoWearableHeadset)
                    notificationHandler?.OnStateChanged(new AudioStateEventArgs(AudioOutputState.Disconnected, actualdevice));
            }


            if (action == BluetoothA2dp.ActionPlayingStateChanged)
            {
                int state = intent.GetIntExtra(BluetoothA2dp.InterfaceConsts.ExtraState, BluetoothAdapter.Error);

                switch (state)
                {
                    case 10:
                        Log.Info(TAG, "Bluetooth A2DP now playing");
                        notificationHandler?.OnStateChanged(new AudioStateEventArgs(AudioOutputState.AudioPlaying, null));
                        break;
                    case 11:
                        Log.Info(TAG, "Bluetooth A2DP has stopped playing");
                        notificationHandler?.OnStateChanged(new AudioStateEventArgs(AudioOutputState.AudioPaused, null));
                        break;
                    default:
                        Log.Warn(TAG, $"Unknown Bluetooth A2DP state: {state}");
                        break;
                }
            }
        }

        internal void Unregister()
        {
            context.UnregisterReceiver(this);
        }

    }

    internal class BondedBluetoothDevice
    {
        private BluetoothDevice device;
        private DeviceClass deviceClass;

        public BluetoothDevice Device { get => device; private set => device = value; }
        public DeviceClass DeviceClass { get => deviceClass; private set => deviceClass = value; }
        public string Address { get => device.Address; }

        public BondedBluetoothDevice(BluetoothDevice device)
        {
            this.deviceClass = device.BluetoothClass?.DeviceClass ?? 0;
            this.device = device;
        }
    }

    internal class AudioStateEventArgs : EventArgs
    {
        private AudioOutputState audioOutputState;
        private BondedBluetoothDevice device;

        public AudioOutputState AudioOutputState { get => audioOutputState; private set => audioOutputState = value; }

        public BondedBluetoothDevice Device { get => device; private set => device = value; }

        internal AudioStateEventArgs(AudioOutputState audioOutputState, BondedBluetoothDevice device)
        {
            this.AudioOutputState = audioOutputState;
            this.Device = device;
        }
    }

    public enum AudioOutputState
    {
        Unknown,
        Connecting,
        Connected,
        Disconnected,
        AudioPlaying,
        AudioPaused,
    }
}
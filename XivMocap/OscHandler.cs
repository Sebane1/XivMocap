// Implementation borrowed from https://github.com/ButterscotchV/AXSlime

using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Everything_To_IMU_SlimeVR.Osc;
using LucHeart.CoreOSC;
using System.Threading.Tasks;
using XivMocap;

namespace Everything_To_IMU_SlimeVR.Osc
{
    public class OscHandler : IDisposable
    {
        public static readonly string BundleAddress = "#bundle";
        public static readonly byte[] BundleAddressBytes = Encoding.ASCII.GetBytes(BundleAddress);
        public static readonly string AvatarParamPrefix = "/avatar/parameters/";
        public static List<string> parameterList = new List<string>();

        private readonly UdpClient _oscClient;

        private readonly CancellationTokenSource _cancelTokenSource = new();
        private Task _oscReceiveTask;
        private bool _disposed = false;

        public bool Disposed { get => _disposed; }

        public OscHandler()
        {
            _oscClient = new UdpClient(39539);
            Task.Run(() => {
                _oscReceiveTask = OscReceiveTask(_cancelTokenSource.Token);
            });
        }

        private static bool IsBundle(ReadOnlySpan<byte> buffer)
        {
            return buffer.Length > 16 && buffer[..8].SequenceEqual(BundleAddressBytes);
        }
        
        /// <summary>
        /// Takes in an OSC bundle package in byte form and parses it into a more usable OscBundle object.
        /// </summary>
        /// <param name="msg"></param>
        /// <returns>Bundle containing elements and a timetag</returns>
        public static OscBundle ParseBundle(Span<byte> msg)
        {
            ReadOnlySpan<byte> msgReadOnly = msg;
            var messages = new List<OscMessage>();
            var index = 0;
            var message = OscMessage.ParseMessage(msg);
            messages.Add(message);
            var output = new OscBundle((ulong)DateTime.Now.Ticks, messages.ToArray());
            return output;
        }
        private async Task OscReceiveTask(CancellationToken cancelToken = default)
        {
            while (!_disposed)
            {
                try
                {
                    var packet = await _oscClient.ReceiveAsync();
                    if (IsBundle(packet.Buffer))
                    {
                        var bundle = ParseBundle(packet.Buffer);
                        if (bundle.Timestamp > DateTime.Now)
                        {
                            // Wait for the specified timestamp
                            _ = Task.Run(
                                async () =>
                                {
                                    await Task.Delay(bundle.Timestamp - DateTime.Now, cancelToken);
                                    OnOscBundle(bundle);
                                },
                                cancelToken
                            );
                        }
                        else
                        {
                            OnOscBundle(bundle);
                        }
                    }
                    else
                    {
                        OnOscMessage(OscMessage.ParseMessage(packet.Buffer));
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
        }

        private void OnOscBundle(OscBundle bundle)
        {
            foreach (var message in bundle.Messages)
            {
                OnOscMessage(message);
            }
        }

        private void OnOscMessage(OscMessage message)
        {
            //if (message.Arguments.Length <= 0)
            //{
            //    return;
            //}
            Plugin.Log.Verbose(message.Address);
            foreach(float item in message.Arguments)
            {
                Plugin.Log.Verbose(item.ToString());
            }
        }

        public void Dispose()
        {
            _cancelTokenSource?.Cancel();
            _cancelTokenSource?.Dispose();
            _oscClient?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

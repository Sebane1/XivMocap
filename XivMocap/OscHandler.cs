// Implementation borrowed from https://github.com/ButterscotchV/AXSlime

using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using LucHeart.CoreOSC;
using System.Threading.Tasks;
using XivMocap;
using System.Linq;

namespace Everything_To_IMU_SlimeVR.Osc
{
    public class OscHandler : IDisposable
    {
        public static readonly string BundleAddress = "#bundle\0";
        public static readonly byte[] BundleAddressBytes = Encoding.ASCII.GetBytes(BundleAddress);
        public static readonly string AvatarParamPrefix = "/avatar/parameters/";
        public static List<string> parameterList = new List<string>();

        private readonly UdpClient _oscClient;
        private UdpClient _udpSender;
        private readonly CancellationTokenSource _cancelTokenSource = new();
        private Task _oscReceiveTask;
        private bool _disposed = false;
        private ulong _timetag;

        public bool Disposed { get => _disposed; }

        public OscHandler()
        {
            _oscClient = new UdpClient(39539);
            _udpSender = new UdpClient();
            _udpSender.Connect("localhost", 39540);
            Task.Run(() =>
            {
                _oscReceiveTask = OscReceiveTask(_cancelTokenSource.Token);
            });
            Task.Run(() =>
            {
                while (true)
                {
                    // Throwing stuff at the wall, send some stuff back to slime in case it tells us we exist.
                    var message = new OscMessage(@"/VMC/Ext/OK", 3, 0, 1);
                    var message2 = new OscMessage(@"/VMC/Ext/Set/Req");
                    var oscBundle = new OscBundle(_timetag++, message, message2);
                    _udpSender.SendAsync(oscBundle.GetBytes());
                    Thread.Sleep(100);
                }
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
                    ProcessBundle(packet.Buffer, cancelToken);
                }
                catch (OperationCanceledException) { }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
        }

        private void ProcessBundle(byte[] buffer, CancellationToken cancelToken = default)
        {
            if (IsBundle(buffer))
            {
                var bundle = ParseBundle(buffer);
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
                OnOscMessage(OscMessage.ParseMessage(buffer));
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
            var loweredAddress = message.Address.ToLower();
            if (loweredAddress.Contains("/vmc/ext/ok"))
            {
                foreach (int item in message.Arguments)
                {
                    Plugin.Log.Verbose(item.ToString());
                }
            }
            else if (loweredAddress.Contains("/vmc/ext/t"))
            {
                foreach (float item in message.Arguments)
                {
                    Plugin.Log.Verbose(item.ToString());
                }
            }
            else if (loweredAddress.Contains("/vmc/ext/bone/pos"))
            {
                foreach (float item in message.Arguments)
                {
                    Plugin.Log.Verbose(item.ToString());
                }
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

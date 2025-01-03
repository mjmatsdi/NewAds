using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwinCAT.Ads;
using TwinCAT.TypeSystem;

namespace NewAds
{
    internal class MsgMonitor
    {
        private bool ConnActive;
        private uint notificationHandle;
        private AdsClient client;

        public delegate void NewMsg(string text);
        public event NewMsg MsgEvent;

        public MsgMonitor() {
            ConnActive = false;
            client = new AdsClient();
        }

        public void Start() {
            if (!ConnActive) {
                client.Connect(AmsNetId.Local, 851);
                var state = client.ReadState();
                Trace.WriteLine("Start() -> Ads State = " + state.AdsState.ToString());
                client.AdsStateChanged += Client_AdsStateChanged;
            }
        }

        public void Stop() {
            if (ConnActive) {
                Trace.WriteLine("    removing .Ready tag monitoring");
                client.TryDeleteDeviceNotification(notificationHandle);
                client.TryDeleteVariableHandle(notificationHandle);
                client.AdsNotification -= Client_AdsNotification;
                client.CleanupSymbolTable();

                if (client.IsConnected) client.Disconnect();
                ConnActive = false;
            }
        }
        private void Client_AdsStateChanged(object? sender, AdsStateChangedEventArgs e) {
            // note that this will not fire if you haven't connected
            Trace.WriteLine("AdsStateChanged(" + e.State.AdsState.ToString() + ")");

            if (sender == null) return;     // we need the AdsClient to do anything

            if (e.State.AdsState == AdsState.Run) {
                if (!ConnActive) {
                    // register for notifications
                    client.AdsNotification += Client_AdsNotification;
                    int size = sizeof(bool);
                    Trace.WriteLine("    adding .Ready tag monitoring");
                    notificationHandle = client.AddDeviceNotification("vMessages.Msgs_SCP.Ready", size, new NotificationSettings(AdsTransMode.OnChange, 10, 0), null);
                    ConnActive = true;
                }
            }
            if (e.State.AdsState == AdsState.Stop) {
                Stop();
                Start();
            }
        }
        private void Client_AdsNotification(object sender, AdsNotificationEventArgs e) {
            Byte readyFlag = e.Data.Span[0];

            if (readyFlag > 0 && sender != null) {
                Trace.WriteLine("New buffer data");
                AdsClient client = (AdsClient)sender;
                uint ackHandle = client.CreateVariableHandle("vMessages.Msgs_SCP.Ack");
                uint bufferHandle = client.CreateVariableHandle("vMessages.Msgs_SCP.Sending");

                try {
                    // Read the PLC buffer
                    int byteSize = 4000;
                    PrimitiveTypeMarshaler converter = new PrimitiveTypeMarshaler(StringMarshaler.DefaultEncoding);
                    byte[] buffer = new byte[byteSize];
                    int readBytes = client.Read(bufferHandle, buffer.AsMemory());
                    string value = null;
                    converter.Unmarshal<string>(buffer.AsSpan(), out value);
                    //Console.WriteLine("Buffer [" + value + "]");
                    string[] msgs = value.Split("<", StringSplitOptions.RemoveEmptyEntries);
                    foreach(string s in msgs) MsgEvent("<" + s);

                    client.WriteAny(ackHandle, true);
                }
                catch (Exception ex) {
                    Trace.WriteLine("Client_AdsNotification() -> exception when processing sending buffer");
                    Trace.WriteLine("    " + ex.Message);
                }
                finally {
                    client.DeleteVariableHandle(bufferHandle);
                    client.DeleteVariableHandle(ackHandle);
                }

            }
        }
    }
}

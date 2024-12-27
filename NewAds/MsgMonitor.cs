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
                //client.AdsStateChanged += Client_AdsStateChanged;
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
                    //Trace.WriteLine("registering for the AdsNotification event");
                    client.AdsNotification += Client_AdsNotification;

                    int size = sizeof(bool);
                    Trace.WriteLine("    adding .Ready tag monitoring");
                    notificationHandle = client.AddDeviceNotification("vMessages.Msgs_SCP.Ready", size, new NotificationSettings(AdsTransMode.OnChange, 10, 0), null);
                    //Trace.WriteLine("    returned handle " + notificationHandle.ToString());
                    ConnActive = true;
                }
            }
            if (e.State.AdsState == AdsState.Stop) {
                Stop();
                Start();
            }
        }
        private void Client_AdsNotification(object sender, AdsNotificationEventArgs e) {
            Trace.WriteLine("New buffer data");
            //Trace.WriteLine("Client_AdsNotification()");
            //Trace.WriteLine("    e.handle " + e.Handle.ToString());
            //Trace.WriteLine("    e.Data.Span[0] = " + e.Data.Span[0].ToString());
            //if (e.UserData != null)
            //    Trace.WriteLine("    e.UserData = " + e.UserData.ToString());

            Byte readyFlag = e.Data.Span[0];

            if (readyFlag > 0 && sender != null) {
                AdsClient client = (AdsClient)sender;
                //Trace.WriteLine("    creating handle for the Ack tag");
                uint ackHandle = client.CreateVariableHandle("vMessages.Msgs_SCP.Ack");
                //Trace.WriteLine("    creating handle for the buffer tag");
                uint bufferHandle = client.CreateVariableHandle("vMessages.Msgs_SCP.Sending");

                try {
                    // Read the PLC buffer
                    int byteSize = 4000; // the buffer is actually 4K
                    PrimitiveTypeMarshaler converter = new PrimitiveTypeMarshaler(StringMarshaler.DefaultEncoding);
                    byte[] buffer = new byte[byteSize];
                    //Trace.WriteLine("    reading buffer tag");
                    int readBytes = client.Read(bufferHandle, buffer.AsMemory());
                    string value = null;
                    converter.Unmarshal<string>(buffer.AsSpan(), out value);
                    //Console.WriteLine("Buffer [" + value + "]");
                    MsgEvent(value);

                    //Trace.WriteLine("    setting the Ack tag = TRUE");
                    client.WriteAny(ackHandle, true);
                }
                catch {
                    Trace.WriteLine("    exception when processing sending buffer");
                }
                finally {
                    //Trace.WriteLine("    deleting handle for the buffer tag");
                    client.DeleteVariableHandle(bufferHandle);
                    //Trace.WriteLine("    deleting handle for the ack tag");
                    client.DeleteVariableHandle(ackHandle);
                }

            }
        }
    }
}

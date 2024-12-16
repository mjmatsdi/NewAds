using System.Buffers.Binary;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TwinCAT.Ads;
using TwinCAT.TypeSystem;
using System.Diagnostics;

/*
    TODO - what is this: "Ads Error: 1 : [AdsClient:TwinCAT.Ads.Internal.INotificationReceiver.OnNotificationError()] Exception: Could not load type 'Invalid_Token.0x020000CF' from assembly 'NewAds, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'."

*/
namespace NewAds
{
    internal class Program
    {
        static bool Quit = false;
        static bool AdsIsRunning;
        static uint ackHandle;
        static uint bufferHandle;

        static async Task Main(string[] args) {
            //ReadInt();
            //ReadString();
            //Thread.Sleep(5000);

            //CancellationToken cancel = CancellationToken.None;
            try {
                AdsClient client = new AdsClient();
                // Connect to target
                client.Connect(AmsNetId.Local, 851);
                client.AdsStateChanged += Client_AdsStateChanged;

                // Add the Notification event handler
                client.AdsNotification += Client_AdsNotification;
                while (!Quit) {
                    int size = sizeof(bool);
                    //ResultHandle result = await client.AddDeviceNotificationAsync("vMessages.Msgs_SCP.Ready", size, new NotificationSettings(AdsTransMode.OnChange, 10, 0), null, cancel);
                    uint notificationHandle = client.AddDeviceNotification("vMessages.Msgs_SCP.Ready", size, new NotificationSettings(AdsTransMode.OnChange, 10, 0), null);

                    //int i = 0;
                    // wait indefinitely
                    //while (client.ReadState().AdsState == AdsState.Run) {
                    while (AdsIsRunning) {
                        //i++;
                        //Console.WriteLine(i.ToString() + " : state = " + adsState.AdsState.ToString() + " : Connected = " + client.IsConnected.ToString());   // always says "Run"
                        //Console.WriteLine(i.ToString() + " : state = " + adsState.DeviceState.ToString());   // always says "0"
                        Thread.Sleep(1000);
                    }
                    client.TryDeleteDeviceNotification(notificationHandle);
                    //client.DeleteDeviceNotification(notificationHandle);
                    //client.DeleteDeviceNotification(result.Handle);
                    Thread.Sleep(2000);
                }
                //client.AdsNotification -= Client_AdsNotification;
            }
            catch (TwinCAT.Ads.AdsErrorException e) {
                Console.WriteLine("Ads was NOT happy: " + e.Message);
            }
            catch {
                Console.WriteLine("Not compatible with TwinCAT ADS.");
            }

        }


        static async void ReadInt() {
            int j = 99;
            using (AdsClient client = new AdsClient()) {
                CancellationToken cancel = CancellationToken.None;
                uint varHandle = 0;
                client.Connect(AmsNetId.Local, 851);

                // the below works to read a TwinCAT INT
                // note that the example had the two tags below as "int". That did not work and I had
                // to change it to Int16. Now it works.
                Int16 intToWrite = 69;
                Int16 intToRead = 0;
                ResultHandle resultHandle = await client.CreateVariableHandleAsync("MAIN.nCounter", cancel);
                varHandle = resultHandle.Handle;
                if (resultHandle.Succeeded) {
                    try {
                        ResultValue<Int16> resultRead = await client.ReadAnyAsync<Int16>(varHandle, cancel);

                        if (resultRead.Succeeded) {
                            intToRead = resultRead.Value;
                            Console.WriteLine("ADS read the value: " + intToRead.ToString());
                        }
                        else {
                            Console.WriteLine("ADS read INT failed.");
                        }

                        ResultWrite resultWrite = await client.WriteAnyAsync(varHandle, intToWrite, cancel);
                    }
                    finally {
                        // Unregister VarHandle after Use
                        ResultAds result = await client.DeleteVariableHandleAsync(varHandle, cancel);
                    }
                }
            }
        }
        static async void ReadString() {
            using (AdsClient client = new AdsClient()) {
                CancellationToken cancel = CancellationToken.None;
                uint varHandle = 0;
                client.Connect(AmsNetId.Local, 851);
                // the below works to read a string
                // note that I kept the examples size of 81 even tho the Sending buffer is 4K and it still works
                ResultHandle resultHandle = await client.CreateVariableHandleAsync("vMessages.Msgs_SCP.Sending", cancel);
                if (resultHandle.Succeeded) {
                    try {
                        // Read ANSI String string[80]
                        int byteSize = 81; // Size of 80 ANSI chars + /0 (STRING[80])
                        PrimitiveTypeMarshaler converter = new PrimitiveTypeMarshaler(StringMarshaler.DefaultEncoding);
                        byte[] buffer = new byte[byteSize];

                        ResultRead resultRead = await client.ReadAsync(resultHandle.Handle, buffer.AsMemory(), cancel);

                        if (resultRead.Succeeded) {
                            string value = null;
                            converter.Unmarshal<string>(buffer.AsSpan(), out value);
                            Console.WriteLine("Send buffer= [" + value + "]");

                            byte[] writeBuffer = new byte[byteSize];
                            // Write ANSI String string[80]
                            value = "Changed";
                            converter.Marshal(value, writeBuffer);
                            ResultWrite resultWrite = await client.WriteAsync(resultHandle.Handle, writeBuffer, cancel);
                        }
                    }
                    finally {
                        ResultAds r1 = await client.DeleteVariableHandleAsync(resultHandle.Handle, cancel);
                    }

                }
            }
        }
        //static async Task RegisterNotificationsAsync() {
        //    CancellationToken cancel = CancellationToken.None;

        //    using (AdsClient client = new AdsClient()) {
        //        // Add the Notification event handler
        //        client.AdsNotification += Client_AdsNotification2;

        //        // Connect to target
        //        client.Connect(AmsNetId.Local, 851);
        //        uint notificationHandle = 0;

        //        // Notification to a DINT Type (UINT32)
        //        // Check for change every 200 ms

        //        int size = sizeof(Int32);

        //        ResultHandle result = await client.AddDeviceNotificationAsync("MAIN.nCounter", size, new NotificationSettings(AdsTransMode.OnChange, 200, 0), null, cancel);

        //        if (result.Succeeded) {
        //            notificationHandle = result.Handle;
        //            await Task.Delay(5000); // Wait asynchronously without blocking the UI Thread.
        //                                    // Unregister the Event / Handle
        //            ResultAds result2 = await client.DeleteDeviceNotificationAsync(notificationHandle, cancel);
        //        }
        //        client.AdsNotification -= Client_AdsNotification2;
        //    }
        //}

        private static void Client_AdsStateChanged(object? sender, AdsStateChangedEventArgs e) {
            Trace.WriteLine("AdsStateChanged(" + e.State.AdsState.ToString() + ")");
            AdsIsRunning = (e.State.AdsState == AdsState.Run);
        }

        static void Client_AdsNotification(object sender, AdsNotificationEventArgs e) {
            Byte readyFlag = e.Data.Span[0];

            if (readyFlag > 0 && sender != null) {
                AdsClient client = (AdsClient)sender;
                if (ackHandle == 0)
                    ackHandle = client.CreateVariableHandle("vMessages.Msgs_SCP.Ack");
                if (bufferHandle == 0)
                    bufferHandle = client.CreateVariableHandle("vMessages.Msgs_SCP.Sending");

                try {
                    // Read the PLC buffer
                    int byteSize = 4000; // the buffer is actually 4K
                    PrimitiveTypeMarshaler converter = new PrimitiveTypeMarshaler(StringMarshaler.DefaultEncoding);
                    byte[] buffer = new byte[byteSize];
                    int readBytes = client.Read(bufferHandle, buffer.AsMemory());
                    string value = null;
                    converter.Unmarshal<string>(buffer.AsSpan(), out value);
                    Console.WriteLine("Buffer [" + value + "]");
                    if (value.ToUpper() == "QUIT") Quit = true;

                    client.WriteAny(ackHandle, true);
                }
                catch {
                    Trace.WriteLine("exception when processing sending buffer");
                    client.DeleteVariableHandle(bufferHandle);
                    client.DeleteVariableHandle(ackHandle);
                }

            }

            // If Synchronization is needed (e.g. in Windows.Forms or WPF applications)
            // we could synchronize via SynchronizationContext into the UI Thread
            /*
             SynchronizationContext syncContext = SynchronizationContext.Current;
              _context.Post(status => someLabel.Text = nCounter.ToString(), null); // Non-blocking post 
            */
        }
    }
}

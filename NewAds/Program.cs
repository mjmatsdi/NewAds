using System.Buffers.Binary;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TwinCAT.Ads;
using TwinCAT.TypeSystem;
using System.Diagnostics;
using System.Drawing;

/*
    TODO - what is this: "Ads Error: 1 : [AdsClient:TwinCAT.Ads.Internal.INotificationReceiver.OnNotificationError()] Exception: Could not load type 'Invalid_Token.0x020000CF' from assembly 'NewAds, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null'."

*/
namespace NewAds
{
    internal class Program
    {
        static bool Quit = false;
        static bool AdsIsRunning;
        static uint notificationHandle;

        static void Main(string[] args) {
            MsgMonitor mm = new MsgMonitor();
            mm.MsgEvent += Mm_MsgEvent;
            mm.Start();

            while (!Quit) {
                try {
                    Thread.Sleep(500);
                    //AdsClient client = new AdsClient();

                    //// you only need to register for the event once, even if you disconnect later and reconnect
                    //Trace.WriteLine("registering for the AdsStateChanged event");
                    //client.AdsStateChanged += Client_AdsStateChanged;

                    //// Connect to target
                    //Trace.WriteLine("client.Connect()");
                    //client.Connect(AmsNetId.Local, 851);


                    //while (true) {
                    //    Thread.Sleep(1000);
                    //}

                }
                catch (Exception e) {
                    Console.WriteLine(e.Message);
                }
            }
            mm.Stop();
        }

        private static void Mm_MsgEvent(string text) {
            Console.WriteLine(text);
            Quit = (text.ToLower() == "quit");
        }

        private static void Client_AdsStateChanged(object? sender, AdsStateChangedEventArgs e) {
            // note that this will not fire if you haven't connected
            Trace.WriteLine("AdsStateChanged(" + e.State.AdsState.ToString() + ")");
            AdsIsRunning = (e.State.AdsState == AdsState.Run);

            if (sender == null) return;     // we need the AdsClient to do anything

            AdsClient client = (AdsClient)sender;
            if (e.State.AdsState == AdsState.Run) {
                if (!client.IsConnected) client.Connect(AmsNetId.Local, 851);
                Thread.Sleep(100);
                // register for notifications
                Trace.WriteLine("registering for the AdsNotification event");
                client.AdsNotification += Client_AdsNotification;

                int size = sizeof(bool);
                Trace.WriteLine("adding ready tag to the notification");
                notificationHandle = client.AddDeviceNotification("vMessages.Msgs_SCP.Ready", size, new NotificationSettings(AdsTransMode.OnChange, 10, 0), null);
                Trace.WriteLine("    returned handle " + notificationHandle.ToString());

            }
            if (e.State.AdsState == AdsState.Stop) {
                client.TryDeleteDeviceNotification(notificationHandle);
                client.TryDeleteVariableHandle(notificationHandle);
                client.AdsNotification -= Client_AdsNotification;
                client.CleanupSymbolTable();
                if (client.IsConnected) {
                    client.Disconnect();
                    Thread.Sleep(100);
                    client.Connect(AmsNetId.Local, 851);
                }
            }
        }




        //static async Task Main(string[] args) {

        //    //CancellationToken cancel = CancellationToken.None;
        //    try {
        //        AdsClient client = new AdsClient();
        //        // Connect to target
        //        Trace.WriteLine("client.Connect()");
        //        client.Connect(AmsNetId.Local, 851);


        //        // I still want to do this even tho the docs say not to. Need to clarify with Beckhoff
        //        Trace.WriteLine("registering for the AdsStateChanged event");
        //        client.AdsStateChanged += Client_AdsStateChanged;

        //        // supposed to trigger when the plc program has been restarted
        //        Trace.WriteLine("registering for the AdsNotificationsInvalidated event");
        //        client.AdsNotificationsInvalidated += Client_AdsNotificationsInvalidated;
        //        Trace.WriteLine("registering for the AdsSymbolVersionChanged event");
        //        client.AdsSymbolVersionChanged += Client_AdsSymbolVersionChanged;

        //        // Add the Notification event handler
        //        Trace.WriteLine("registering for the AdsNotification event");
        //        client.AdsNotification += Client_AdsNotification;       // used with the buffer ready event

        //        AdsIsRunning = true;    // testing a theory

        //        while (!Quit) {
        //            int size = sizeof(bool);
        //            Trace.WriteLine("adding ready tag to the notification");
        //            notificationHandle = client.AddDeviceNotification("vMessages.Msgs_SCP.Ready", size, new NotificationSettings(AdsTransMode.OnChange, 10, 0), null);
        //            Trace.WriteLine("    returned handle " + notificationHandle.ToString());

        //            while (AdsIsRunning && notificationHandle > 0) {
        //                Thread.Sleep(1000);
        //            }
        //            Trace.WriteLine("Inner while loop exited.");
        //            Trace.WriteLine("    AdsIsRunning = " + AdsIsRunning.ToString());
        //            Trace.WriteLine("    notificationHandle = " + notificationHandle.ToString());

        //            if (notificationHandle > 0) {
        //                Trace.WriteLine("deregistering the notificationHandle");
        //                client.DeleteDeviceNotification(notificationHandle);
        //            }
        //            Thread.Sleep(2000);
        //        }
        //        Trace.WriteLine("deregistering the AdsNotification");
        //        client.AdsNotification -= Client_AdsNotification;
        //    }
        //    catch (TwinCAT.Ads.AdsErrorException e) {
        //        Console.WriteLine("Ads was NOT happy: " + e.Message);
        //    }
        //    catch {
        //        Console.WriteLine("Not compatible with TwinCAT ADS.");
        //    }

        //}

        //private static void Client_AdsSymbolVersionChanged(object? sender, AdsSymbolVersionChangedEventArgs e) {
        //    Trace.WriteLine("AdsSymbolVersionChanged");
        //    if (sender != null) {
        //        Trace.WriteLine("    getting the AdsClient object");
        //        AdsClient client = (AdsClient)sender;
        //        Trace.WriteLine("    calling CleanupSymbolTable");
        //        client.CleanupSymbolTable();
        //        Trace.WriteLine("    setting notificationHandle = 0");
        //        notificationHandle = 0;
        //    }
        //}

        //private static void Client_AdsNotificationsInvalidated(object? sender, AdsNotificationsInvalidatedEventArgs e) {
        //    Trace.WriteLine("Client_AdsNotificationsInvalidated()");
        //    Trace.WriteLine("    setting notificationHandle to 0");
        //    notificationHandle = 0;
        //}

        //private static void Client_AdsStateChanged(object? sender, AdsStateChangedEventArgs e) {
        //    Trace.WriteLine("AdsStateChanged(" + e.State.AdsState.ToString() + ")");
        //    //AdsIsRunning = (e.State.AdsState == AdsState.Run);
        //}

        static void Client_AdsNotification(object sender, AdsNotificationEventArgs e) {
            Trace.WriteLine("Client_AdsNotification()");
            Trace.WriteLine("    e.handle " + e.Handle.ToString());
            Trace.WriteLine("    e.Data.Span[0] = " + e.Data.Span[0].ToString());
            if (e.UserData != null)
                Trace.WriteLine("    e.UserData = " + e.UserData.ToString());

            Byte readyFlag = e.Data.Span[0];

            if (readyFlag > 0 && sender != null) {
                AdsClient client = (AdsClient)sender;
                Trace.WriteLine("    creating handle for the Ack tag");
                uint ackHandle = client.CreateVariableHandle("vMessages.Msgs_SCP.Ack");
                Trace.WriteLine("    creating handle for the buffer tag");
                uint bufferHandle = client.CreateVariableHandle("vMessages.Msgs_SCP.Sending");

                try {
                    // Read the PLC buffer
                    int byteSize = 4000; // the buffer is actually 4K
                    PrimitiveTypeMarshaler converter = new PrimitiveTypeMarshaler(StringMarshaler.DefaultEncoding);
                    byte[] buffer = new byte[byteSize];
                    Trace.WriteLine("    reading buffer tag");
                    int readBytes = client.Read(bufferHandle, buffer.AsMemory());
                    string value = null;
                    converter.Unmarshal<string>(buffer.AsSpan(), out value);
                    Console.WriteLine("Buffer [" + value + "]");
                    if (value.ToUpper() == "QUIT") Quit = true;

                    Trace.WriteLine("    setting the Ack tag = TRUE");
                    client.WriteAny(ackHandle, true);
                }
                catch {
                    Trace.WriteLine("    exception when processing sending buffer");
                }
                finally {
                    Trace.WriteLine("    deleting handle for the buffer tag");
                    client.DeleteVariableHandle(bufferHandle);
                    Trace.WriteLine("    deleting handle for the ack tag");
                    client.DeleteVariableHandle(ackHandle);
                }

            }
        }


        // testing functions based in whole or part from Beckhoffs examples
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


    }
}

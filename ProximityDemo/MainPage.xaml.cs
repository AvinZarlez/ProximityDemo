using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

//Add Proxm
using Windows.Networking.Proximity;

namespace ProximityDemo
{
    /// <summary>
    /// A page that showcases use of the Proximity API
    /// </summary>
    public sealed partial class MainPage : Page
    {

        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;

            #region ProximityDevice example initialization
            //For sending/receive messages
            _proximityDevice = ProximityDevice.GetDefault();
            if (_proximityDevice == null)
            {
                WriteMessageText("Failed to initialized proximity device.\n" +
                                 "Your device may not have proximity hardware.");
            }
            #endregion
        }

        #region ProximityDevice example

        // Proximity Device
        private Windows.Networking.Proximity.ProximityDevice _proximityDevice;
        // Published Message ID
        private long _publishedMessageID = -1;
        // Subscribed Message ID
        private long _subscribedMessageID = -1;

        private void messageReceived(Windows.Networking.Proximity.ProximityDevice device,
                                     Windows.Networking.Proximity.ProximityMessage message)
        {
            WriteMessageText("Message receieved: " + message.DataAsString + "\n");
        }

        private void SubscribeButtonPressed(object sender, RoutedEventArgs e)
        {

            if (_subscribedMessageID == -1)
            {
                _subscribedMessageID = _proximityDevice.SubscribeForMessage("Windows.ExampleMessage", messageReceived);
                WriteMessageText("Now subscribing! Tap devices to receive.");
            }
            else
            {
                WriteMessageText("Already subscribing!\n");
            }
        }

        private void StopSubscribingButtonPressed(object sender, RoutedEventArgs e)
        {
            _proximityDevice.StopSubscribingForMessage(_subscribedMessageID);
            _subscribedMessageID = -1;
            WriteMessageText("Stopped subscribing");
        }

        private void PublishButtonPressed(object sender, RoutedEventArgs e)
        {

            //Stop Publishing the current message.
            if (_publishedMessageID != -1)
                _proximityDevice.StopPublishingMessage(_publishedMessageID);

            string msg = "String here";//MessageTextBox.Text;
            if (msg.Length > 0)
            {
                _publishedMessageID = _proximityDevice.PublishMessage("Windows.ExampleMessage", msg);
                WriteMessageText("Publishing message! Tap devices to send.");
            }
            else
            {
                WriteMessageText("Error: Write something first, silly!");
            }
        }

        private void StopPublishingButtonPressed(object sender, RoutedEventArgs e)
        {
            _proximityDevice.StopPublishingMessage(_publishedMessageID);
            _publishedMessageID = -1;
            WriteMessageText("Stopped publishing");
        }

        #endregion

        private int pivot_index = 0;
        private void PivotChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (pivot_index)
            {
                case 0:
                    StopSubscribingButtonPressed(null, null);
                    StopPublishingButtonPressed(null,null);
                    break;
                case 1:
                    // Detach the callback handler (there can only be one PeerConnectProgress handler).
                    PeerFinder.TriggeredConnectionStateChanged -= TriggeredConnectionStateChanged;
                    // Detach the incoming connection request event handler.
                    PeerFinder.ConnectionRequested -= ConnectionRequested;
                    if (_started)
                    {
                        PeerFinder.Stop();
                        CloseSocket();
                        _started = false;
                    }
                    WriteMessageText("Stopping PeerFinder");
                    break;
            }

            pivot_index = (((Pivot)sender).SelectedIndex);

            #region PeerFinder socket example initialization/deconstructor
            if (pivot_index == 1)
            {
                if ((PeerFinder.SupportedDiscoveryTypes &
                        PeerDiscoveryTypes.Triggered) ==
                        PeerDiscoveryTypes.Triggered)
                {
                    PeerFinder.TriggeredConnectionStateChanged +=
                        TriggeredConnectionStateChanged;
                }
                PeerFinder.ConnectionRequested += ConnectionRequested;
                WriteMessageText("Starting PeerFinder");
            }
            #endregion
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Prepare page for display here.

            DisplayNameTextBox.Text = PeerFinder.DisplayName;

        }

        // Handle external connection requests.
        PeerInformation requestingPeer;

        private void ConnectionRequested(object sender,
            ConnectionRequestedEventArgs e)
        {
            requestingPeer = e.PeerInformation;
            WriteMessageText("Connection requested by " + requestingPeer.DisplayName + ". " +
                "Click 'Accept Connection' to connect.");
        }

        // Write a message to MessageBlock on the UI thread.
        private Windows.UI.Core.CoreDispatcher messageDispatcher = Window.Current.CoreWindow.Dispatcher;

        async private void WriteMessageText(string message, bool overwrite = false)
        {
            await messageDispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                () =>
                {
                    message = DateTime.Now.ToString("[HH:mm:ss] ") + message + "\n";
                        
                    if (overwrite)
                        MessageBlock.Text = message;
                    else
                        MessageBlock.Text = message + MessageBlock.Text;
                });
        }


        Windows.Networking.Sockets.StreamSocket proximitySocket;
        Windows.Storage.Streams.DataWriter dataWriter;
        bool _started = false;

        // Reference socket streams for writing and reading messages.
        private void SendMessage(Windows.Networking.Sockets.StreamSocket socket)
        {
            // PeerFinder has not been started.
            if (!_started)
            {
                CloseSocket();
                return;
            }

            // Get the network socket from the proximity connection.
            proximitySocket = socket;

            // Create DataWriter for writing messages to peers.
            dataWriter = new Windows.Storage.Streams.DataWriter(proximitySocket.OutputStream);

            // Listen for messages from peers.
            Windows.Storage.Streams.DataReader dataReader =
                    new Windows.Storage.Streams.DataReader(proximitySocket.InputStream);
            StartReader(proximitySocket, dataReader);
        }

        private void CloseSocket()
        {
            if (proximitySocket != null)
            {
                proximitySocket.Dispose();
                proximitySocket = null;
            }

            if (dataWriter != null)
            {
                dataWriter.Dispose();
                dataWriter = null;
            }
        }

        public void Dispose()
        {
            CloseSocket();
        }

        // Read out and print the message received from the socket.
        private async void StartReader(Windows.Networking.Sockets.StreamSocket socket,
           Windows.Storage.Streams.DataReader reader)
        {
            try
            {
                uint bytesRead = await reader.LoadAsync(sizeof(uint));
                if (bytesRead > 0)
                {
                    uint strLength = (uint)reader.ReadUInt32();
                    bytesRead = await reader.LoadAsync(strLength);
                    if (bytesRead > 0)
                    {
                        String message = reader.ReadString(strLength);
                        WriteMessageText("Received message: " + message);
                        StartReader(socket, reader); // Start another reader
                    }
                    else
                    {
                        WriteMessageText("The peer app closed the socket");
                        reader.Dispose();
                        CloseSocket();
                    }
                }
                else
                {
                    WriteMessageText("The peer app closed the socket");
                    reader.Dispose();
                    CloseSocket();
                }
            }
            catch
            {
                WriteMessageText("The peer app closed the socket");
                reader.Dispose();
                CloseSocket();
            }
        }



        private void SendButtonPressed(object sender, RoutedEventArgs e)
        {
            if (proximitySocket != null)
            {
                SendMessageText();
            }
            else
            {
                WriteMessageText("You must enter proximity to send a message.");
            }
        }

        // Send a message to the socket.
        private async void SendMessageText()
        {
            string msg = "String here";//MessageTextBox.Text;

            if (msg.Length > 0)
            {
                var msgLength = dataWriter.MeasureString(msg);
                dataWriter.WriteInt32(msg.Length);
                dataWriter.WriteString(msg);
                try
                {
                    await dataWriter.StoreAsync();
                    WriteMessageText("Message sent: " + msg);
                }
                catch (Exception e)
                {
                    WriteMessageText("Send error: " + e.Message);
                    CloseSocket();
                }
            }
            else
            {
                WriteMessageText("Error: Write something first, silly!");
            }
        }


        private void TriggeredConnectionStateChanged(
            object sender,
            TriggeredConnectionStateChangedEventArgs e)
        {
            if (e.State == TriggeredConnectState.PeerFound)
            {
                WriteMessageText("Peer found. You may now pull your devices out of proximity.");
            }
            if (e.State == TriggeredConnectState.Completed)
            {
                WriteMessageText("Connected. You may now send a message.");
                SendMessage(e.Socket);
            }
        }


        // Start Advertising for peers.
        // Display message if Discovery or Tap is not supported.
        private void AdvertiseButtonPressed(object sender, RoutedEventArgs e)
        {

            if (_started)
            {
                WriteMessageText("You are already advertising for a connection.");
                return;
            }

            PeerFinder.DisplayName = DisplayNameTextBox.Text;

            if ((PeerFinder.SupportedDiscoveryTypes &
                 PeerDiscoveryTypes.Triggered) ==
                 PeerDiscoveryTypes.Triggered)
            {

                WriteMessageText("You can tap to connect a peer device that is " +
                                 "also advertising for a connection.");
            }
            else
            {
                WriteMessageText("Tap to connect is not supported.");
            }

            if ((PeerFinder.SupportedDiscoveryTypes &
                 PeerDiscoveryTypes.Browse) !=
                 PeerDiscoveryTypes.Browse)
            {
                WriteMessageText("Peer discovery using Wi-Fi Direct is not supported.");
            }

            PeerFinder.Start();
            _started = true;
        }

        // Click event handler for "Browse" button.
        async private void FindButtonPressed(object sender, RoutedEventArgs e)
        {
            if ((PeerFinder.SupportedDiscoveryTypes & PeerDiscoveryTypes.Browse) != PeerDiscoveryTypes.Browse)
            {
                WriteMessageText("Peer discovery using Wi-Fi is not supported.");
                return;
            }

            try
            {
                var peerInfoCollection = await PeerFinder.FindAllPeersAsync();
                if (peerInfoCollection.Count > 0)
                {
                    // Connect to the first peer
                    // NOTE: In production, would provide a list
                    ConnectToPeer(peerInfoCollection[0]);
                }
            }
            catch (Exception err)
            {
                WriteMessageText("Error finding peers: " + err.Message);
            }
        }

        private void AcceptButtonPressed(object sender, RoutedEventArgs e)
        {
            if (requestingPeer == null)
            {
                WriteMessageText("No peer connection has been requested.");
                return;
            }

            ConnectToPeer(requestingPeer);
        }


        async private void ConnectToPeer(PeerInformation peerInfo)
        {
            WriteMessageText("Peer found. Connecting to " + peerInfo.DisplayName);
            try
            {
                Windows.Networking.Sockets.StreamSocket socket =
                    await PeerFinder.ConnectAsync(peerInfo);

                WriteMessageText("Connection successful. You may now send messages.");
                SendMessage(socket);
            }
            catch (Exception err)
            {
                WriteMessageText("Connection failed: " + err.Message);
            }

            requestingPeer = null;
        }

        // Click event handler for "Stop" button.
        private void StopButtonPressed(object sender, RoutedEventArgs e)
        {
            _started = false;
            PeerFinder.Stop();
            if (proximitySocket != null) { CloseSocket(); }
            WriteMessageText("Stopped PeerFinder and closed socket");
        }
        
    }
}

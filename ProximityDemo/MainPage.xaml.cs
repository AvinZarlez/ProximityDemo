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

// Add Proxmity API on top of defaults
using Windows.Networking.Proximity;

namespace ProximityDemo
{
    /// <summary>
    /// A page that showcases use of the Proximity API
    /// </summary>
    public sealed partial class MainPage : Page
    {
        #region General elements

        #region General variables

        // Last pivot index
        private int last_pivot_index = -1;
        // Dispatcher for messages we display to the screen
        private Windows.UI.Core.CoreDispatcher messageDispatcher = Window.Current.CoreWindow.Dispatcher;

        #endregion //Variables

        /// <summary>
        /// Page constructor
        /// </summary>]
        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;

            Application.Current.Suspending += new SuspendingEventHandler(OnSuspending);
 
            #region ProximityDevice example initialization
            //For sending and receiving messages
            _proximityDevice = ProximityDevice.GetDefault();
            if (_proximityDevice == null)
            {
                WriteMessageText("Failed to initialized proximity device.\n" +
                                 "Your device may not have proximity hardware.");
            }
            #endregion
        }

        /// <summary>
        /// Writes a message to MessageBlock on the UI thread.
        /// </summary>
        /// <param name="message">The message to be added. Automatically includes a new line.</param>
        /// <param name="overwrite">Should this message clear all the other messages from the screen?</param>
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

        /// <summary>
        /// Invoked when Pivot element is changed/swiped.
        /// Sets up the next example, cleans up the last example.
        /// </summary>
        private void PivotChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (last_pivot_index)
            {
                case 0: //Leaving Proximity Device demo
                    StopSubscribingButtonPressed(null, null);
                    StopPublishingButtonPressed(null,null);
                    break;
                case 1: //Leaving Proximity Device demo
                    // Detach the callback handler (there can only be one PeerConnectProgress handler).
                    PeerFinder.TriggeredConnectionStateChanged -= TriggeredConnectionStateChanged;
                    // Detach the incoming connection request event handler.
                    PeerFinder.ConnectionRequested -= ConnectionRequested;
                    if (_advertising)
                    {
                        PeerFinder.Stop();
                        CloseSocket();
                        _advertising = false;
                    }
                    WriteMessageText("Stopping PeerFinder");
                    break;
            }

            last_pivot_index = (((Pivot)sender).SelectedIndex);

            #region PeerFinder socket example initialization/deconstructor
            if (last_pivot_index == 1) // Initialize PeerFinder.
            {
                // If supported, add handler
                if ((PeerFinder.SupportedDiscoveryTypes & PeerDiscoveryTypes.Triggered) == PeerDiscoveryTypes.Triggered)
                {
                    PeerFinder.TriggeredConnectionStateChanged += TriggeredConnectionStateChanged;
                }
                PeerFinder.ConnectionRequested += ConnectionRequested;
                WriteMessageText("Starting PeerFinder");
            }
            #endregion
        }

        /// <summary>
        /// Invoked when leaving this page. Clean up our variables
        /// NOTE: If MainPage is only page, this is not invoked!
        /// </summary>
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            Dispose();
        }

        /// <summary>
        /// Invoked when app suspends. Clean up our variables
        /// </summary>
        private async void OnSuspending(Object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            WriteMessageText("App is suspending, disposing");

            Dispose();
        }

        /// <summary>
        /// Dispose of this page 
        /// </summary>
        public void Dispose()
        {

            PeerFinder.Stop();
            _advertising = false;

            CloseSocket(); //Cleans up proximitySocket and dataWriter

            if (_proximityDevice != null)
            {
                if (_subscribedMessageID != -1) //Stop Subscribing
                {
                    _proximityDevice.StopSubscribingForMessage(_subscribedMessageID);
                    _subscribedMessageID = -1;
                }
                if (_publishedMessageID != -1) //Stop publishing
                {
                    _proximityDevice.StopPublishingMessage(_publishedMessageID);
                    _publishedMessageID = -1;
                }
            }
        }

        #endregion //General elements

        #region ProximityDevice example

        #region ProximityDevice variables

        // Proximity Device
        private ProximityDevice _proximityDevice;
        // Published Message ID
        private long _publishedMessageID = -1;
        // Subscribed Message ID
        private long _subscribedMessageID = -1;

        #endregion //Variables

        #region ProximityDevice functions

        /// <summary>
        /// Invoked when a message is received
        /// </summary>
        /// <param name="device">The ProximityDevice object that received the message.</param>
        /// <param name="message">The message that was received.</param>
        private void messageReceived(ProximityDevice device, ProximityMessage message)
        {
            WriteMessageText("Message receieved: " + message.DataAsString);
        }

        #endregion //ProximityDevice functions

        #region ProximityDevice example buttons
        /// <summary>
        /// When the Subscribe button is pressed, start listening for other devices.
        /// </summary>
        private void SubscribeButtonPressed(object sender, RoutedEventArgs e)
        {

            if (_subscribedMessageID == -1)
            {
                _subscribedMessageID = _proximityDevice.SubscribeForMessage("Windows.ExampleMessage", messageReceived);
                WriteMessageText("Subscribing for messages! Tap devices to receive.");
            }
            else
            {
                WriteMessageText("Already subscribing!");
            }
        }

        /// <summary>
        /// When the stop subscribing button is pressed, stop listening for other devices.
        /// </summary>
        private void StopSubscribingButtonPressed(object sender, RoutedEventArgs e)
        {
            if (_subscribedMessageID != -1)
            {
                _proximityDevice.StopSubscribingForMessage(_subscribedMessageID);
                _subscribedMessageID = -1;
                WriteMessageText("Stopped subscribing");
            }
        }

        /// <summary>
        /// When the Publish button is pressed, start broadcasting a message on the Windows.ExampleMessage channel.
        /// </summary>
        private void PublishButtonPressed(object sender, RoutedEventArgs e)
        {

            //Stop Publishing the current message.
            if (_publishedMessageID != -1)
                _proximityDevice.StopPublishingMessage(_publishedMessageID);

            string msg = TapMessageTextBox.Text;
            if (msg.Length > 0)
            {
                _publishedMessageID = _proximityDevice.PublishMessage("Windows.ExampleMessage", msg);
                WriteMessageText("Publishing your message. Tap devices to send!");
            }
            else
            {
                WriteMessageText("Error: Write something first, silly!");
            }
        }

        /// <summary>
        /// When the stop publishing button is pressed, stop broadcasting.
        /// </summary>
        private void StopPublishingButtonPressed(object sender, RoutedEventArgs e)
        {
            if (_publishedMessageID != -1)
            {
                _proximityDevice.StopPublishingMessage(_publishedMessageID);
                _publishedMessageID = -1;
                WriteMessageText("Stopped publishing");
            }
        }
        #endregion //ProximityDevice example buttons

        #endregion //ProximityDevice example

        #region ProximityFinder example

        #region ProximityFinder variables

        // Handle external connection requests.
        PeerInformation requestingPeer;
        // Current peer socket and datawriter
        Windows.Networking.Sockets.StreamSocket proximitySocket;
        Windows.Storage.Streams.DataWriter dataWriter;
        // Are we currently advertising?
        bool _advertising = false;

        #endregion //Variables

        #region ProximityFinder functions

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// Set the default value for the Display Name textbox
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Prepare Proximity Finder example device name textbox
            DisplayNameTextBox.Text = PeerFinder.DisplayName;

        }

        /// <summary>
        /// Handler for when the connection state changes
        /// </summary>
        private void TriggeredConnectionStateChanged(object sender, TriggeredConnectionStateChangedEventArgs e)
        {
            if (e.State == TriggeredConnectState.PeerFound)
            {
                WriteMessageText("Peer found. You may now pull your devices out of proximity.");
            }
            if (e.State == TriggeredConnectState.Completed)
            {
                WriteMessageText("You are now connected and may exchange messages!");
                EnableMessaging(e.Socket);
            }
        }

        /// <summary>
        /// When another device requests a connection, tell us!
        /// </summary>
        private void ConnectionRequested(object sender, ConnectionRequestedEventArgs e)
        {
            requestingPeer = e.PeerInformation;
            WriteMessageText("Connection requested by " + requestingPeer.DisplayName + ". " +
                "Click 'Accept Connection' to connect.");
        }

        /// <summary>
        /// Connect to a peer
        /// </summary>
        /// <param name="peerInfo">Information about the peer requesting to connect.</param>
        async private void ConnectToPeer(PeerInformation peerInfo)
        {
            WriteMessageText("Peer found. Connecting to " + peerInfo.DisplayName);
            try
            {
                Windows.Networking.Sockets.StreamSocket socket =
                    await PeerFinder.ConnectAsync(peerInfo);

                WriteMessageText("Connection successful. You may now send messages.");
                EnableMessaging(socket);
            }
            catch (Exception err)
            {
                WriteMessageText("Connection failed: " + err.Message);
            }

            requestingPeer = null;
        }

        /// <summary>
        /// Define socket and data writer for sending and reading messages.
        /// </summary>
        private void EnableMessaging(Windows.Networking.Sockets.StreamSocket socket)
        {
            // If PeerFinder has not been started, quit.
            if (!_advertising)
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
            StartReader(dataReader);
        }

        /// <summary>
        /// Read out and print messages from the DataReader
        /// </summary>
        /// <param name="reader">The current DataReader</param>
        private async void StartReader( Windows.Storage.Streams.DataReader reader )
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
                        StartReader(reader); // Start another reader
                    }
                    else
                    {
                        CloseReader(reader, "No string length!");
                    }
                }
                else
                {
                    CloseReader(reader, "No bytes read!");
                }
            }
            catch
            {
                CloseReader(reader);
            }
        }

        /// <summary>
        /// Close the reader, triggered from the StartReader function
        /// </summary>
        /// <param name="reader">The current DataReader</param>
        /// <param name="err">The error message (default blank)</param>
        private void CloseReader( Windows.Storage.Streams.DataReader reader, string err = "")
        {
            WriteMessageText("The peer app closed the socket. "+err);
            reader.Dispose();
            CloseSocket();
        }

        /// <summary>
        /// Close the current socket and datawriter
        /// </summary>
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

        /// <summary>
        /// Send the current message to peer
        /// </summary>
        private async void SendMessageText()
        {
            string msg = MessageTextBox.Text;

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

        #endregion //ProximityFinder functions

        #region ProximityFinder example buttons

        /// <summary>
        /// When the Advertise button is pressed, start to advertise for peers.
        /// Display an message if "Discovery" or "Tap" types are not supported.
        /// </summary>
        private void AdvertiseButtonPressed(object sender, RoutedEventArgs e)
        {

            if (_advertising)
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
            _advertising = true;
        }

        /// <summary>
        /// When the Find button is pressed, find other devices advertising their connections.
        /// </summary>
        async private void FindButtonPressed(object sender, RoutedEventArgs e)
        {
            if ((PeerFinder.SupportedDiscoveryTypes & PeerDiscoveryTypes.Browse) != PeerDiscoveryTypes.Browse)
            {
                WriteMessageText("Peer discovery using Wi-Fi is not supported.");
                return;
            }

            try
            {
                WriteMessageText("Attempting to find connection.");
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

        /// <summary>
        /// When the Accept button is pressed, agree to connect to a peer whose request we have already received.
        /// </summary>
        private void AcceptButtonPressed(object sender, RoutedEventArgs e)
        {
            if (requestingPeer == null)
            {
                WriteMessageText("No peer connection has been requested.");
                return;
            }

            ConnectToPeer(requestingPeer);
        }

        /// <summary>
        /// When the Stop button is pressed, stop advertising and close sockets
        /// </summary>
        private void StopButtonPressed(object sender, RoutedEventArgs e)
        {
            _advertising = false;
            PeerFinder.Stop();
            CloseSocket();
            WriteMessageText("Stopped PeerFinder and closed socket");
        }

        /// <summary>
        /// When the Send button is pressed, call SendMessageText.
        /// </summary>
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

        #endregion //ProximityFinder example buttons

        #endregion //ProximityFinder example
    }
}

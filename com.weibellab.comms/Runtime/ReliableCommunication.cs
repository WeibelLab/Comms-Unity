using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Comms
{

    /// <summary>
    /// TCP Server/Client for strings/json/bytes
    /// 
    /// v1.3.3
    /// - RemoteEndpointIP()
    /// - RemoteEndpointPort()
    /// - CloseClientConnection()
    /// 
    /// v1.32
    /// - Calling CloseConnection() would not always raise the disconnected event
    /// 
    /// v1.31
    /// - Fixed bug where OnConnect event was called after OnMessageReceived events (subscribers would see this as a bug)
    /// 
    /// v1.3
    /// - ConnectOnEnable is now a public member
    /// - StartConnection() should be called to start listening / connecting to the server
    ///  (when ConnectOnEnable is false)
    /// - No exception or error message is thrown when socket has been shutdown (happens on local
    /// connections)
    /// 
    /// v1.2
    /// - Version history; Version Tag ReliableCommunication.VERSION
    /// - add isConnected member
    /// 
    /// v1.1
    /// - onConnect, onDisconnect, OnError events
    /// - When sockets disconnect gracefully, it doesn't log an error anymore
    /// - Fixed Send() when message size is not dynamic
    /// 
    /// v1.0
    /// - First version ;)
    /// 
    /// Authors: Danilo Gaques, Tommy Sharkey
    /// </summary>
    /// 
    public class ReliableCommunication : Comms
    {

        #region public members
        // Determining type of socket communication
        public bool isServer;
        public int ListenPort;

        // events
        public ReliableCommunicationSocketConnected OnConnect;
        private bool onConnectRaised = false;
        public ReliableCommunicationSocketDisconnected OnDisconnect;
        private bool onDisconnectRaised = false;

        // this might need a queue
        //public ReliableCommunicationSocketError OnError;
        //private bool onSocketErrorRaised = false;

        // future work
        // using a custom processing pipeline ?
        //public bool customProcessingCallback = false;
        //public bool useRegularEventCallback = true;

        // message headers
        public CommunicationHeaderType messageFormat = CommunicationHeaderType.Dynamic;
        public int fixedMessageLength { 
            get
            {
                if (messageFormat == CommunicationHeaderType.Fixed)
                {
                    if (this.headerFormat.IsArray && this.headerFormat.list[0].HasField("length"))
                    {
                        return (int)this.headerFormat.list[0]["length"].i;
                    }
                    else
                    {
                        fixedMessageLength = 1024;
                        return fixedMessageLength;
                    }
                }

                return -1;
            }
            set
            {
                this.HeaderFormat = "[{\"name\": \"data\", \"length\":" + value.ToString() + "}]";
            }
        }



        // Timeout
        [HideInInspector]
        public int ReconnectTimeoutMs = 1000;
        public int ListenErrorTimeoutMs = 5000;
        [HideInInspector]
        public int WaitToAbortMs = 100;

        [Tooltip("Check this box if the socket should connect when the script / game object is enabled / first starts")]
        public bool ConnectOnEnable = true;

        // External Connection Information
        public TargettingStrategy strategy = TargettingStrategy.Web;
        public string id = "qwerty";
        public string key = "inkoay";
        public string room = "blue-dog";
        public string webserverSyncAddress = "http://localhost:3000/join";
        private Thread ipSettingThread;

        #endregion

        #region private members
        // Host
        public CommunicationEndpoint Host = new CommunicationEndpoint("192.168.1.1", 12345, "");

        // TCP
        private TcpListener selfServer;
        private TcpClient tcpClient;

        // Shared
        private Thread listenerThread;

        // name used for the purpose of logging
        private string LogName;
        #endregion


        /// <summary>
        /// This boolean is true whenever a client is connected to the server
        /// or when the client is connected to a server
        /// </summary>
        bool _isConnected = false;
        public bool isConnected
        {
            get
            {
                return _isConnected;
            }
        }


        #region UnityEvents
        /// <summary>
        /// Called whenever the behavior is initialized by the application
        /// </summary>
        new void Awake()
        {
            base.Awake();

            if (Host.Name.Length == 0)
                Host.Name = this.gameObject.name;

            LogName = "[" + Host.Name + (isServer ? " TCP Server] - " : " TCP Client] - ");

            // making sure sockets report statistics regardless of how they were instantiated
            statisticsReporter = GetComponent<CommunicationStatistics>();
            if (statisticsReporter == null)
            {
                //Debug.LogWarning(LogName + " Missing socket statistics companion");
                statisticsReporter = this.gameObject.AddComponent<CommunicationStatistics>();
                statisticsReporter.Name = Host.Name;
                statisticsReporter.TCP = true;
            }
        }

        // Update is called once per frame
        new protected void Update()
        {
            // onConnectEvents should always come before any messages
            if (onConnectRaised)
            {
                _isConnected = true;

                if (OnConnect != null)
                    OnConnect.Invoke(this);

                onConnectRaised = false;
            }


            base.Update();


            // onDisconnectEvents should be passed after all messages are sent to clients
            if (onDisconnectRaised)
            {
                _isConnected = false;

                if (OnDisconnect != null)
                    OnDisconnect.Invoke(this);

                onDisconnectRaised = false;
            }


        }

        private void OnEnable()
        {
            if (ConnectOnEnable) {
                this.setTargetAndStartConnection();
            }
        }

        private void OnDisable()
        {
            CloseConnection();
        }

        #endregion


        public string RemoteEndpointIP()
        {
            if (tcpClient != null)
            {
                return ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();
            }

            return "";
        }

        public int RemoteEndpointPort()
        {
            if (tcpClient != null)
            {
                return ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Port;
            }

            return -1;
        }

        /// <summary>
        /// Call this method when you want to start a connection (either listening as a server
        /// or connecting as a client).
        /// 
        ///  if `ConnectOnEnable` is checked / True, StartConnection will be called automatically for you ;)
        /// 
        /// </summary>
        public void StartConnection()
        {
            if (listenerThread != null && listenerThread.IsAlive)
            {
                Debug.LogWarning(LogName + "Already running. Call Disconnect() first or  ForceReconnect() instead");
                return;
            }
            killThreadRequested = false;

            try
            {
                if (isServer)
                    listenerThread = new Thread(new ThreadStart(ServerLoopThread));
                else
                    listenerThread = new Thread(new ThreadStart(ClientLoopThread));
                listenerThread.IsBackground = true;
                listenerThread.Start();
            }
            catch (Exception e)
            {
                Debug.LogError(LogName + " Failed to start socket thread: " + e);
            }
        }

        /// <summary>
        /// Sets the target IP and Port based on set strategy
        /// By default, the user manually sets the port and IP.
        /// Optionally, they can ping a web server to get other device info.
        /// </summary>
        private async void setTargetAndStartConnection()
        {
            // yield return null;
            if (Application.isPlaying) {
                Task<bool> task;
                switch (this.strategy)
                {
                    case TargettingStrategy.Manual:
                        this.StartConnection();
                        break;
                    case TargettingStrategy.Web:
                        task = this.getAddressFromWeb();
                        await task;
                        while (task.Result == false && Application.isPlaying) {
                            Debug.Log(LogName + "Failed to Connect");
                            Task.Delay(1000).Wait();
                            task = this.getAddressFromWeb();
                            await task;
                        }
                        this.StartConnection();
                        break;
                    case TargettingStrategy.UDP:
                        task = this.getAddressFromUdp();
                        await task;
                        while (task.Result == false && Application.isPlaying) {
                            Debug.Log(LogName + "Failed to Connect");
                            Task.Delay(100).Wait();
                            task = this.getAddressFromUdp();
                            await task;
                        }
                        this.StartConnection();
                        break;
                    default:
                        throw new Exception(LogName + "Unknown Connection Strategy");
                }
            }
        }

        /// <summary>
        /// Sets the target Port and IP from a web request.
        /// POSTs data about this Reliable Communication
        /// gets a response with server/client address(es)
        /// </summary>
        private async Task<bool> getAddressFromWeb()
        {
            Debug.Log(LogName + "Syncing with web server");
            System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
            var postData = new Dictionary<string, string>{
                { "id", this.id },
                { "type", this.isServer? "server":"client" },
                { "key", this.key },
                { "room", this.room },
                { "port", this.isServer? this.ListenPort.ToString(): "" }
            };

            // Make POST request
            var content = new System.Net.Http.FormUrlEncodedContent(postData);
            System.Net.Http.HttpResponseMessage response = await client.PostAsync(this.webserverSyncAddress, content);
            string responseString = await response.Content.ReadAsStringAsync();

            // Set IP address
            if (response.IsSuccessStatusCode) {
                JSONObject responseJson = new JSONObject(responseString);
                if (this.isServer) {
                    // Do Nothing - let the clients connect to you
                }
                else {
                    // Parse IP Address
                    String ip = responseJson["ip"].str;
                    Regex rg = new Regex(@"[0-9]*\.[0-9]*\.[0-9]*\.[0-9]*");
                    MatchCollection matches = rg.Matches(ip);
                    if (matches.Count > 0) {ip = matches[0].Value;}
                    this.Host.Address = ip;
                    Debug.Log(LogName + "Set IP Address to " + ip);

                    // Parse Port
                    this.Host.Port = (int) responseJson["port"].i;
                }
                return true;
            }
            // Handle errors
            else {
                switch (response.StatusCode) {
                    case HttpStatusCode.BadRequest: // 400
                        Debug.LogError(LogName + "Made a bad request\n" + response.ToString());
                        // throw new Exception(LogName + "Made a bad request\n" + response.ToString())
                        break;
                    case HttpStatusCode.Forbidden: // 403
                        Debug.LogError(LogName + "Invalid Passkey: " + this.key + "\n" + response.ToString());
                        // throw new Exception(LogName + "Invalid Passkey: " + this.key + "\n" + response.ToString());
                        break;
                    case HttpStatusCode.NotFound: // 404
                        // will happen if server hasn't setup the room yet
                        // Debug.LogError(LogName + "Couldn't find room: " + this.room + "\n" + response.ToString());
                        break;
                    default:
                        Debug.LogError(LogName + "Request Failed: " + response.StatusCode + "\n" + response.ToString());
                        // throw new Exception(LogName + "Request Failed: " + response.StatusCode + "\n" + response.ToString());
                        break;
                }
                
                return false;
            }
        }

        private async Task<bool> getAddressFromUdp()
        {
            await Task.Delay(1);
            throw new NotImplementedException(); // TODO: Implement
            // return false;
        }


        /// <summary>
        /// Closes the client connection (or the server)
        /// </summary>
        public void CloseConnection()
        {

            // Asks thread to stop listening
            killThreadRequested = true;

            // close all sockets
            try
            {
                if (tcpClient != null)
                {
                    tcpClient.Close();
                    tcpClient = null;
                }


                if (selfServer != null)
                {
                    selfServer.Stop();
                    selfServer = null;
                }
            }
            catch (NullReferenceException)
            {
                // do nothing
            }


            // Stop Thread if it is still running
            if (listenerThread != null && listenerThread.IsAlive)
            {

                try
                {
                    listenerThread.Join(WaitToAbortMs);
                    if (listenerThread.IsAlive)
                        listenerThread.Abort();

                    listenerThread = null;
                }
                catch (Exception)
                {
                    // don't care
                }

            }

            // Is it connected? then update all members
            if (_isConnected)
            {
                _isConnected = false;
                onDisconnectRaised = false; // we are not sure that there will be another update loop

                // make sure others are aware that this socket disconnected
                if (OnDisconnect != null)
                    OnDisconnect.Invoke(this);
            }

        }

        /// <summary>
        /// This method closes a client connection without restarting threads (if ReliableCommunication is
        /// a server). If ReliableCommunication is a client, then this method has the same effect as CloseConnection();
        /// 
        /// Eventually, ReliableCommunication server's will support more clients. 
        /// For now, it only supports one and provides a similar interface for both sides.
        /// 
        /// That will change with v2 (or maybe we will introduce a ReliableCommunicationServer
        /// to make these changes not break ReliableCommunication).
        /// 
        /// Anyhow, if one needs to drop a client (and get another client onboard), this method
        /// is the one that should be called!
        /// </summary>
        public void CloseClientConnection()
        {
            if (!this.isServer)
            {
                CloseConnection();
            }
            else
            {
                if (tcpClient != null)
                {
                    if (tcpClient.Connected)
                    {
                        tcpClient.Close();
                    }
                }
            }
        }

        /// <summary>
        /// Restarts server / reconnects client
        /// </summary>
        public void ForceReconnect()
        {
            CloseConnection();
            this.setTargetAndStartConnection();
        }


        public void Send(JSONObject msg)
        {
            Send(msg.ToString());
        }

        public void Send(string msg)
        {
            byte[] msgAsBytes = Encoding.UTF8.GetBytes(msg);
            Send(msgAsBytes);
        }

        public void Send(byte[] msg)
        {
            if (tcpClient == null)
            {
                Debug.LogWarning(LogName + "not connected! Dropping message...");
                return;
            }

            // Build message with Headers
            byte[] bytesToSend;
            switch (messageFormat)
            {
                case CommunicationHeaderType.Dynamic:
                    byte[] messageLength = BitConverter.GetBytes((UInt32)msg.Length);
                    bytesToSend = new byte[msg.Length + sizeof(UInt32)];
                    Buffer.BlockCopy(messageLength, 0, bytesToSend, 0, sizeof(UInt32));
                    Buffer.BlockCopy(msg, 0, bytesToSend, sizeof(UInt32), msg.Length);
                    break;
                case CommunicationHeaderType.Fixed:
                    bytesToSend = new byte[fixedMessageLength];
                    if (msg.Length > fixedMessageLength)
                    {
                        Debug.LogError(string.Format("{0}Message is too large! (Expected {1}, received {2} bytes)", LogName, fixedMessageLength, msg.Length));
                    }
                    else
                    {
                        Buffer.BlockCopy(msg, 0, bytesToSend, 0, msg.Length);
                    }
                    break;
                case CommunicationHeaderType.Custom:
                default:
                    bytesToSend = msg;
                    break;

            }

            // Send Message
            try
            {
                // Get a stream object for writing.
                NetworkStream stream;
                if (this.isServer)
                {
                    stream = tcpClient.GetStream(); // FIXME: creating Error on HoloLens
                }
                else
                {
                    stream = tcpClient.GetStream();
                }

                // Send
                if (stream.CanWrite)
                {
                    stream.Write(bytesToSend, 0, bytesToSend.Length);
                    statisticsReporter.RecordMessageSent(bytesToSend.Length);
                }
            }
            catch (SocketException e)
            {
                Debug.Log(LogName + " Socket Exception while sending: " + e);
            }
            catch (ObjectDisposedException e)
            {
                Debug.LogWarning(LogName + " Tried to send message through stream that was closed/closing. Dropping message and forcing stream to close.\n" + e);
                tcpClient.Close();
                tcpClient = null;
            }
        }


        #region ReliableCommunication client/server implementation
        private void ClientLoopThread()
        {
            bool firstTime = true;
            bool socketConnected = false;

            while (!killThreadRequested)
            {
                try
                {
                    socketConnected = false;
                    if (!firstTime)
                        Thread.Sleep(ReconnectTimeoutMs);
                    firstTime = false;

                    Debug.Log(LogName + "Connecting to " + Host.Address + ":" + Host.Port);
                    tcpClient = new TcpClient(Host.Address, Host.Port);
                    Debug.Log(String.Format("{0} Connected to {1}:{2}", LogName, Host.Address, Host.Port));
                    statisticsReporter.RecordConnectionEstablished();
                    socketConnected = true;
                    onConnectRaised = true;
                    //socketConnection.ReceiveBufferSize = 1024 * 1024; // 1 mb

                    // handles messages
                    SocketMessageReadingLoop(tcpClient);
                }
                catch (SocketException socketException)
                {
                    switch (socketException.SocketErrorCode)
                    {
                        case SocketError.Interrupted:
                            return; // we were forcefully canceled - free thread
                        case SocketError.Shutdown:
                            return; // we forcefully freed the socket, so yeah, we will get an error
                        case SocketError.TimedOut:
                            if (killThreadRequested)
                                Debug.LogError(LogName + "timed out");
                            else
                                Debug.LogError(LogName + "timed out - Trying again in " + (ReconnectTimeoutMs / 1000f) + " sec");
                            statisticsReporter.RecordStreamError();
                            break;
                        case SocketError.ConnectionRefused:
                            if (killThreadRequested)
                                Debug.LogError(LogName + "connection refused! Are you sure the server is running?");
                            else
                                Debug.LogError(LogName + "connection refused! Are you sure the server is running? - Trying again in " + (ReconnectTimeoutMs / 1000f) + " sec");
                            statisticsReporter.RecordStreamError();
                            break;
                        case SocketError.NotConnected:
                            // this sounds extra, but sockets that never connected will die with NotConnected
                            if (socketConnected)
                            {
                                Debug.LogError(LogName + " Socket Exception: " + socketException.SocketErrorCode + "->" + socketException);
                                statisticsReporter.RecordStreamError();
                            }
                            break;
                        default:
                            // if we didn't interrupt it -> reconnect, report statistics, log warning
                            Debug.LogError(LogName + " Socket Exception: " + socketException.SocketErrorCode + "->" + socketException);
                            statisticsReporter.RecordStreamError();
                            break;
                    }

                }
                catch (ObjectDisposedException)
                {
                    // this exception happens when the socket could not finish  its operation
                    // and we forcefully aborted the thread and cleared the object
                }
                catch (ThreadAbortException)
                {
                    // this exception happens when the socket could not finish  its operation
                    // and we forcefully aborted the thread (we wait 100 ms)
                }
                catch (SocketDisconnected)
                {
                    // this is our very own exception for when a client disconnects during a read
                    // do nothing.. finally will take care of it below
                }
                catch (Exception e)
                {
                    // this is likely not a socket error. So while we do not record a stream error,
                    // we still log for later learning about it
                    Debug.LogWarning(LogName + "Exception " + e);
                }
                finally
                {
                    if (socketConnected)
                    {
                        if (killThreadRequested)
                            Debug.Log(LogName + "Disconnected");
                        else
                            Debug.Log(LogName + "Disconnected - Trying again in " + (ReconnectTimeoutMs / 1000f) + " sec");
                        statisticsReporter.RecordStreamDisconnect();
                        onDisconnectRaised = true;
                    }
                }
            }
        }

        private void ServerLoopThread()
        {
            bool firstTime = true;
            bool clientConnected = false;
            bool serverListening = false;
            while (!killThreadRequested)
            {
                try
                {
                    serverListening = false;
                    if (!firstTime)
                    {
                        Thread.Sleep(ListenErrorTimeoutMs);
                    }
                    firstTime = false;


                    selfServer = new TcpListener(IPAddress.Parse("0.0.0.0"), ListenPort);
                    selfServer.Start();
                    Debug.Log(string.Format("{0} Listening at 0.0.0.0:{1}", LogName, ListenPort));
                    serverListening = true;

                    // Get a stream object for reading
                    // Todo: Handle multiple clients
                    while (!killThreadRequested && selfServer != null)
                    {
                        try
                        {
                            using (tcpClient = selfServer.AcceptTcpClient())
                            {
                                Debug.Log(string.Format("{0}Client connected {1}:{2}", LogName, ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString(), ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Port));
                                clientConnected = true;
                                onConnectRaised = true;
                                statisticsReporter.RecordConnectionEstablished();
                                SocketMessageReadingLoop(tcpClient, true);
                            }
                        }
                        catch (SocketException socketException)
                        {
                            if (socketException.SocketErrorCode != SocketError.Interrupted)
                            {
                                // if we didn't interrupt it -> reconnect, report statistics, log warning
                                Debug.LogError(LogName + "client socket Exception: " + socketException.SocketErrorCode + "->" + socketException);
                                statisticsReporter.RecordStreamError();
                            }
                            else
                                return; // ends thread (finally will take care of statistics and logging below)
                        }
                        catch (SocketDisconnected)
                        {
                            // do nothing.. finally will take care of it below
                        }
                        finally
                        {
                            if (clientConnected)
                            {
                                statisticsReporter.RecordStreamDisconnect();
                                onDisconnectRaised = true;
                                Debug.Log(LogName + "Client disconnected");
                            }
                            clientConnected = false;
                        }

                    }
                }
                catch (SocketException socketException)
                {
                    if (socketException.SocketErrorCode != SocketError.Interrupted && socketException.SocketErrorCode != SocketError.Shutdown)
                    {
                        // if we didn't interrupt it (or shut it down) -> log warning and report statistics
                        Debug.LogError(LogName + "Server socket Exception: " + socketException.SocketErrorCode + "->" + socketException);
                        statisticsReporter.RecordStreamError();
                        break;
                    }
                    else
                    {
                        return; // ends thread
                    }
                }
                catch (ObjectDisposedException)
                {
                    // this exception happens when the socket could not finish  its operation
                    // and we forcefully aborted the thread and cleared the object
                }
                catch (ThreadAbortException)
                {
                    // this exception happens when the socket could not finish  its operation
                    // and we forcefully aborted the thread (we wait 100 ms)
                }
                catch (Exception e)
                {
                    // this is likely not a socket error. So while we do not record a stream error,
                    // we still log for later learning about it
                    Debug.LogWarning(LogName + "Exception " + e);
                }
                finally
                {
                    // were we listening? 
                    if (serverListening)
                    {
                        if (killThreadRequested)
                            Debug.Log(LogName + "Stopped");
                        else
                            Debug.Log(LogName + "Stopped - Trying again in " + (ReconnectTimeoutMs / 1000f) + " sec");
                    }
                }
            }
        }

        private void SocketMessageReadingLoop(TcpClient tcpClientSocket, bool incoming = false)
        {
            using (NetworkStream stream = tcpClientSocket.GetStream())
            {
                statisticsReporter.RecordConnectionEstablished();
                try
                {
                    while (!killThreadRequested)
                    {
                        var data = this.ParseHeader(stream);
                        statisticsReporter.RecordMessageReceived();
                        lock (messageQueueLock)
                        {
                            messageQueue.Enqueue(data);
                        }
                    }
                }
                catch (System.IO.IOException ioException)
                {
                    // when stream read fails, it throws IOException.
                    // let's expose that exception and handle it below
                    throw ioException.InnerException;
                }
            }
        }


        #endregion

    }

    [System.Serializable]
    public class ReliableCommunicationSocketConnected : UnityEvent<ReliableCommunication> { }

    [System.Serializable]
    public class ReliableCommunicationSocketDisconnected : UnityEvent<ReliableCommunication> { }

    /// <summary>
    /// Called when ReliableCommunicationSocket raises an error (int -> socket error; string -> message converted to text)
    /// </summary>
    [System.Serializable]
    public class ReliableCommunicationSocketError : UnityEvent<ReliableCommunication, int, string> { }
}
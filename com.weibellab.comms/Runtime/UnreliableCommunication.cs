using System;
using System.Collections.Generic;

using System.Net.Sockets;
using System.Threading;
using System.Net;


using UnityEngine;
using System.Text;


namespace Comms
{
    /// <summary>
    /// UDP socket for sending/receiving bytes/string/jsonObject
    /// 
    /// v1.1
    /// - parallel conversion of types (byte[] -> string -> JSONObject happens in the receiving thread)
    /// 
    /// v1.0
    /// - First version ;)
    /// 
    /// Authors: Danilo Gaques
    /// </summary>
    /// 
    public class UnreliableCommunication : MonoBehaviour
    {

        [Tooltip("Used for logs so that we know which class is throwing errors")]
        public string Name;

        [Tooltip("Port number the socket should be using for listening")]
        public int port = 12345;

        [HideInInspector]
        public int WaitToAbortMs = 100;

        // message handlers
        public CommunicationStringEvent StringMessageReceived;
        public CommunicationByteEvent ByteMessageReceived;
        public CommunicationJsonEvent JsonMessageReceived;
        public CommunicationMessageType EventType;

        [HideInInspector]
        private CommunicationMessageType runningEventType; // the type of event that we care about


        [HideInInspector]
        private Queue<object> messageQ = new Queue<object>();
        private object messageQueueLock = new object();

        private UdpClient _impl;
        private string LogName;
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);  // listens to any address
        bool stopThread = false;
        Thread socketThread;

        CommunicationStatistics statisticsReporter;

        [SerializeField]
        public List<CommunicationEndpoint> Targets = new List<CommunicationEndpoint>();

        // ================================================================================ Unity Events =========================================================== //
        /// <summary>
        /// Socket constructor, creates all socket objects and defines each socket unique id
        /// </summary>

        public void Awake()
        {
            if (Name.Length == 0)
                Name = this.gameObject.name;

            LogName = "[" + Name + " UDP] - ";

            // making sure sockets report statistics regardless of how they were instantiated
            statisticsReporter = GetComponent<CommunicationStatistics>();
            if (statisticsReporter == null)
            {
                //Debug.LogWarning(LogName + " Missing socket statistics companion");
                statisticsReporter = this.gameObject.AddComponent<CommunicationStatistics>();
                statisticsReporter.Name = Name;
                statisticsReporter.TCP = false;
            }

        }

        /// <summary>
        /// Called every frame
        /// </summary>
        void Update()
        {
            // are we waiting for messages ?
            if (messageQ.Count > 0)
            {
                // process message received
                Queue<object> tmpQ;

                lock (messageQueueLock)
                {
                    // copies the queue from the thread
                    tmpQ = messageQ;
                    messageQ = new Queue<object>();
                }

                // thread is good to go while we handle its messages
                switch (this.runningEventType)
                {
                    case CommunicationMessageType.Byte:
                        while (tmpQ.Count > 0)
                        {
                            MyByteArrayWrapper msg = (MyByteArrayWrapper)tmpQ.Dequeue();
                            ByteMessageReceived.Invoke(msg.array);
                        }
                        break;

                    case CommunicationMessageType.String:
                        while (tmpQ.Count > 0)
                        {
                            string msg = (string)tmpQ.Dequeue();
                            StringMessageReceived.Invoke(msg);
                        }
                        break;

                    case CommunicationMessageType.Json:
                        while (tmpQ.Count > 0)
                        {
                            JSONObject msg = (JSONObject)tmpQ.Dequeue();
                            JsonMessageReceived.Invoke(msg);
                        }
                        break;
                }

            }
        }

        public void StartServer()
        {
            // sticks to the desired event type (and erases the previous list if the type changed)
            if (runningEventType != EventType)
            {
                messageQ = new Queue<object>();
                runningEventType = EventType;
            }

            // creates the socket
            try
            {
                _impl = new UdpClient(port);
                socketThread = new Thread(new ThreadStart(SocketThreadLoop));
                socketThread.IsBackground = true;
                stopThread = false;
                socketThread.Start();
                Debug.Log(string.Format("{0}Listening on port {1} for {2} events", LogName, port, runningEventType.ToString()));
            }
            catch (Exception err)
            {
                Debug.LogError(string.Format("{0}Unable to start - {1}", LogName, err.ToString()));

                if (socketThread != null)
                    socketThread.Abort();
                socketThread = null;
                stopThread = true;
                _impl = null;
            }
        }

        public void StopServer()
        {
            if (socketThread != null)
            {
                // asks thread to stopp
                stopThread = true;

                // closes socket
                _impl.Close();

                // aborts thread if it is still running
                if (socketThread.IsAlive)
                {
                    socketThread.Join(WaitToAbortMs); // waits WaitToAbortMs ms
                    if (socketThread.IsAlive)
                        socketThread.Abort();
                }

                // reports stats
                statisticsReporter.RecordStreamDisconnect();

                // disposes of objects
                socketThread = null;

                _impl = null;

            }

            // erases queue

            Debug.Log(string.Format("{0}Stopped", LogName));
        }

        public void RestartServer()
        {
            this.StartServer();
            this.StopServer();
        }

        // whenever socket is enabled, we enable the thread (The socket won't run if disabled)
        private void OnEnable()
        {
            this.StartServer();
        }


        // whenever socket is disabled, we stop listening for packets
        private void OnDisable()
        {
            this.StopServer();
        }


        // ==================================== Code that only runs on the Unity Editor / Windows Desktop ======================================================//

        // Windows thread loop
        /// <summary>
        /// Socket Thread Loop for the socket version running on Windows
        /// </summary>
        private void SocketThreadLoop()
        {
            statisticsReporter.RecordConnectionEstablished();
            while (!stopThread)
            {
                try
                {
                    byte[] msg = _impl.Receive(ref anyIP);
                    statisticsReporter.RecordPacketReceived(msg.Length);


                    if (this.runningEventType == CommunicationMessageType.Byte) // Byte Message
                    {
                        lock (messageQueueLock)
                        {
                            messageQ.Enqueue(new MyByteArrayWrapper(msg));
                        }
                    }
                    else
                    {
                        string msgString = Encoding.UTF8.GetString(msg);

                        if (this.runningEventType == CommunicationMessageType.String) // String Message
                        {
                            lock (messageQueueLock)
                            {
                                messageQ.Enqueue(msgString);
                            }
                        }
                        else // Json Message
                        {
                            JSONObject msgJson = new JSONObject(msgString);
                            lock (messageQueueLock)
                            {
                                messageQ.Enqueue(msgJson);
                            }
                        }
                    }

                }
                catch (ThreadAbortException)
                { }
                catch (System.Net.Sockets.SocketException socketException)
                {
                    // if we didn't interrupt it -> reconnect, report statistics, log warning
                    if (socketException.SocketErrorCode != SocketError.Interrupted)
                    {
                        Debug.LogError(LogName + "Socket Error: " + socketException.SocketErrorCode + "->" + socketException);
                        statisticsReporter.RecordStreamError();
                    }

                }
                catch (Exception err)
                {
                    Debug.LogError(LogName + "Generic Exception -> " + err.ToString());
                    statisticsReporter.RecordStreamError();
                }
            }
        }

        public void Broadcast(JSONObject msg)
        {
            this.Broadcast(msg.Print());
        }

        public void Broadcast(string msg)
        {
            this.Broadcast(Encoding.UTF8.GetBytes(msg));
        }

        public void Broadcast(byte[] msg)
        {
            foreach (CommunicationEndpoint target in this.Targets)
            {
                this.SendTo(msg, target.AsIPEndPoint());
            }
        }

        public void SendTo(JSONObject msg, IPEndPoint target)
        {
            SendTo(msg.ToString(), target);
        }

        public void SendTo(string msg, IPEndPoint target)
        {
            byte[] msgAsBytes = Encoding.UTF8.GetBytes(msg);
            SendTo(msgAsBytes, target);
        }

        public void SendTo(byte[] msg, IPEndPoint target)
        {
            (new UdpClient()).SendAsync(msg, msg.Length, target);
        }


        // this is crazy, but this wrapper speeds up conversion from object to byte[] 10x
        // (not sure if the same is valid on IL2CPP, let's see....
        private class MyByteArrayWrapper
        {
            public byte[] array;
            public MyByteArrayWrapper(byte[] a) { array = a; }
        }

    }
}

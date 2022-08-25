using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Comms
{
    /// <summary>
    /// Parent class for Server and Client classes
    /// Provides configuration, logging, events, and queuing
    /// functionality.
    /// </summary>
    [System.Serializable]
    public abstract class Comms
    {
        /// <summary>
        /// "Log Name" the prefix used for all logs from this object
        /// </summary>
        public string LName;
        /// <summary>
        /// Whether to show all logs
        /// if true, prints all logs
        /// if false, only prints errors
        /// </summary>
        public bool Quiet;
        [SerializeField]
        public Config config;

        // Public Events
        public CommunicationByteEvent OnByteMessageReceived;
        public CommunicationStringEvent OnStringMessageReceived;
        public CommunicationJsonEvent OnJsonMessageReceived;
        public SocketConnectedEvent OnClientConnected;
        public SocketDisconnectedEvent OnClientDisconnected;
        public SocketErrorEvent OnClientError;

        // Threads
        protected Thread thread;
        protected bool stopThread;
        public int ThreadAbortTimeout = 100;
        /// <summary>
        /// A threaded function to parse received data.
        /// </summary>
        public System.Action<object> MessageParser;

        // Queue
        protected Queue<byte[]> MessageQueue;
        protected System.Object MessageQueueLock;
        /// <summary>
        /// Allows scripts to queue method calls until Update
        /// </summary>
        protected Queue<System.Action> UnityThreadQueue;
        protected System.Object UnityThreadQueueLock;

        /// <summary>
        /// The default threaded handling of a message.
        /// This may be simply adding messages to the runtime queue
        /// Or accumulating a certain number of bytes before enqueuing
        /// Or replaced by a custom parser that may convert bytes to an image or other behavior.
        /// </summary>
        /// <param name="data"></param>
        abstract protected void DefaultMessageParser(object o);

        public Comms(Config config)
        {
            this.config = config;

            // Create empty event objects
            OnByteMessageReceived = new CommunicationByteEvent();
            OnStringMessageReceived = new CommunicationStringEvent();
            OnJsonMessageReceived = new CommunicationJsonEvent();
            OnClientConnected = new SocketConnectedEvent();
            OnClientDisconnected = new SocketDisconnectedEvent();
            OnClientError = new SocketErrorEvent();

            // Instantiate Queue elements
            MessageQueueLock = new object();
            MessageQueue = new Queue<byte[]>();
            UnityThreadQueue = new Queue<System.Action>();
            UnityThreadQueueLock = new object();
            MessageParser = this.DefaultMessageParser;

            // Set Log Name
            this.LName = $"[Comms:{config.Endpoint.Name}]";
        }

        ~Comms()
        {
            this.StopThread();
            Log("Destroyed");
        }

        virtual public void StopThread()
        {
            if (this.thread != null)
            {
                // Asks thread to stop
                this.stopThread = true;

                // Aborts thread if still running
                if (this.thread.IsAlive)
                {
                    this.thread.Join(ThreadAbortTimeout);
                    if (this.thread.IsAlive)
                        this.thread.Abort();
                }

                // TODO: report statistics
                //stats.RecordStreamDisconnect();

                // Dispose
                this.thread = null;
            }
        }

        protected void ReadFromQueue()
        {
            lock (MessageQueueLock)
            {
                if (MessageQueue.Count == 0) return;
                Log($"Reading {MessageQueue.Count} messages");
                // Parse each message in queue
                if (!config.DropAccumulatedMessages && config.MaxMessagesPerFrame == -1)
                {
                    while (MessageQueue.Count > 0)
                    {
                        ParseData(MessageQueue.Dequeue());
                    }
                }
                // Parse up to MaxMessagesPerFrame
                else if (!config.DropAccumulatedMessages)
                {
                    int i = 0;
                    while (MessageQueue.Count > 0 && i < config.MaxMessagesPerFrame)
                    {
                        ParseData(MessageQueue.Dequeue());
                        i++;
                    }
                }
                // Drop all messages but most recent
                else
                {
                    while (MessageQueue.Count > 1) // leave 1
                    {
                        MessageQueue.Dequeue();
                        // TODO: log dropped packet with statistics reporter
                    }
                    ParseData(MessageQueue.Dequeue());
                }
            }
        }

        protected void RunOnUnityThread(System.Action func)
        {
            lock (UnityThreadQueueLock)
            {
                UnityThreadQueue.Enqueue(func);
            }
        }

        protected void ParseData(byte[] data)
        {
            // TODO: put in try catch if UnityEvents propogate errors
            switch (config.MessageType)
            {
                default:
                case CommunicationMessageType.Byte:
                    OnByteMessageReceived.Invoke(data);
                    break;
                case CommunicationMessageType.String:
                    OnStringMessageReceived.Invoke(Encoding.UTF8.GetString(data));
                    break;
                case CommunicationMessageType.Json:
                    OnJsonMessageReceived.Invoke(new JSONObject(Encoding.UTF8.GetString(data)));
                    break;
            }
        }

        protected void Log(string log)
        {
            if (this.Quiet) return;
            Debug.Log(LName + " " + log);
        }

        protected void LogWarning(string log)
        {
            Debug.LogWarning(LName + " " + log);
        }

        protected void LogError(string log)
        {
            Debug.LogError(LName + " " + log);
        }

        #region Unity Runtime

        // No Awake method on purpose.
        // CommsComponent will instantiate in the OnEnable method
        // (after Awake)

        virtual public void OnEnable()
        {

        }

        virtual public void Reset()
        {

        }

        virtual public void Start()
        {

        }

        virtual public void FixedUpdate()
        {

        }

        virtual public void Update()
        {
            lock (UnityThreadQueueLock)
            {
                while (UnityThreadQueue.Count > 0)
                {
                    UnityThreadQueue.Dequeue().Invoke();
                }
            }
        }

        virtual public void LateUpdate()
        {

        }

        virtual public void OnApplicationQuit()
        {

        }

        virtual public void OnDisable()
        {

        }

        virtual public void OnDestroy()
        {

        }
        #endregion

        /// <summary>
        /// Reads readLength bytes from a network stream and saves it to buffer
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="readLength"></param>
        public static void readToBuffer(NetworkStream stream, byte[] buffer, int readLength)
        {
            int offset = 0;
            // keeps reading until a full message is received
            while (offset < buffer.Length)
            {
                int bytesRead = stream.Read(buffer, offset, readLength - offset); // read from stream
                // TODO: stats.RecordPacketReceived(bytesRead);

                // "  If the remote host shuts down the connection, and all available data has been received,
                // the Read method completes immediately and return zero bytes. "
                // https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.networkstream.read?view=netframework-4.0
                if (bytesRead == 0)
                {
                    throw new SocketDisconnected();// returning here means that we are done
                }

                offset += bytesRead; // updates offset
            }
        }
    }

    [System.Serializable]
    public struct Config
    {
        /// <summary>
        /// IP:Port and human readable name of the connection
        /// </summary>
        public CommunicationEndpoint Endpoint;
        /// <summary>
        /// What kind of messages will be passed? json, string, or bytes
        /// </summary>
        public CommunicationMessageType MessageType;
        /// <summary>
        /// In each Update loop, will only read the most recent message
        /// </summary>
        public bool DropAccumulatedMessages;
        /// <summary>
        /// In each Update loop, will read a max of this many messages.
        /// Does not drop messages, just spreads them out across frames.
        /// Overridden by DropAccumulatedMessages
        /// </summary>
        public int MaxMessagesPerFrame;

        //public Header header; // TODO: implement header



        public static Config Default
        {
            get
            {
                return new Config()
                {
                    Endpoint = new CommunicationEndpoint("0.0.0.0", 3000),
                    MessageType = CommunicationMessageType.Byte,
                    DropAccumulatedMessages = false,
                    MaxMessagesPerFrame = -1
                };
            }
        }
    }
}

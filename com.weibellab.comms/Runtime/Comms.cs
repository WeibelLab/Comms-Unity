using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Comms
{
    /// <summary>
    /// Parent class for Server and Client classes
    /// Provides configuration, logging, events, and queuing
    /// functionality.
    /// </summary>
    public abstract class Comms : MonoBehaviour
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
        public Config config;

        // Public Events
        public CommunicationByteEvent OnByteMessageReceived;
        public CommunicationStringEvent OnStringMessageReceived;
        public CommunicationJsonEvent OnJsonMessageReceived;
        public SocketConnectedEvent OnClientConnected;
        public SocketDisconnectedEvent OnClientDisconnected;
        public SocketErrorEvent OnClientError;

        // Queue
        private Queue<byte[]> MessageQueue;
        private System.Object MessageQueueLock;
        /// <summary>
        /// Allows scripts to queue method calls until Update
        /// </summary>
        protected Queue<System.Action> UnityThreadQueue;
        protected System.Object UnityThreadQueueLock;

        protected void ReadFromQueue()
        {
            lock (MessageQueueLock)
            {
                if (MessageQueue.Count == 0) return;

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

        private void ParseData(byte[] data)
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

        public void EnqueueMessage(byte[] data)
        {
            lock (MessageQueueLock)
            {
                MessageQueue.Enqueue(data);
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
        protected void Awake()
        {
            // Instantiate Queue elements
            MessageQueueLock = new object();
            MessageQueue = new Queue<byte[]>();
            UnityThreadQueue = new Queue<System.Action>();
            UnityThreadQueueLock = new object();

            // Set Log Name
            this.LName = $"[Comms:{config.Endpoint.Name}]";
        }

        protected void Update()
        {
            lock (UnityThreadQueueLock)
            {
                while(UnityThreadQueue.Count > 0)
                {
                    UnityThreadQueue.Dequeue().Invoke();
                }
            }
        }
        #endregion
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
    }
}

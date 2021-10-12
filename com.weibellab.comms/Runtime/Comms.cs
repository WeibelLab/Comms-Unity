using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Comms
{
    public abstract class Comms : MonoBehaviour
    {
        /// <summary>
        /// A JSON that breaks down the different elements in a message.
        /// eg:
        /// [
        ///   {"name": "id", "length": 8 },
        ///   {"name": "foo", "length": 4 },
        ///   {"name": "size", "length": 4, "type": "int" },
        ///   {"name": "img", "length": "size", "type": "bytes" }
        /// ]
        /// 
        /// "length" is the only required element, it can be an integer or the name of another element
        /// "name" is optional, is helpful for parsing messages from a dictionary later, or is required for using formulae
        /// "type" is optional, and is currently unused. (Assumed to be an int if referenced by another element in a formula)
        /// </summary>
        public string HeaderFormat
        {
            get { return this.headerFormat.ToString(true); }
            set { 
                this.headerFormat = new JSONObject(value);
                this.isDefaultFormat = false;
            }
        }
        protected JSONObject headerFormat = new JSONObject();
        protected bool isDefaultFormat;
        public CommunicationMessageType EventType;



        // message handlers
        public CommunicationStringEvent StringMessageReceived;
        public CommunicationByteEvent ByteMessageReceived;
        public CommunicationJsonEvent JsonMessageReceived;
        public CommunicationCustomEvent CustomMessageReceived;


        // Thread variables
        [HideInInspector]
        public Queue<Dictionary<string, byte[]>> messageQueue = new Queue<Dictionary<string, byte[]>>();
        protected System.Object messageQueueLock = new System.Object();
        protected bool killThreadRequested = false;


        // dropping packets?
        [Tooltip("Data events will be called only with the last message received. Use wisely")]
        public bool dropAccumulatedMessages = false;


        // Socket statistics
        protected CommunicationStatistics statisticsReporter;

        /// <summary>
        /// We raise this exception when the socket disconnects mid-read
        /// </summary>
        public class SocketDisconnected : Exception
        {
        }

        public void Awake()
        {
            if (headerFormat is null)
            {
                HeaderFormat = "[{\"name\":\"header\", \"length\":4, \"type\":\"int\"},{\"name\":\"data\", \"length\":\"header\"}]";
                this.isDefaultFormat = true;

                // static header size
                //int msgSize = 1024;
                //HeaderFormat = "[{\"name\": \"data\", \"length\":"+msgSize.ToString()+"}]";
            }

            Debug.Log(HeaderFormat);
        }

        protected Dictionary<string, byte[]> ParseHeader(NetworkStream stream)
        {
            var ret = new Dictionary<string, byte[]>();

            List<JSONObject> format = this.headerFormat.list;

            for (int i=0; i<format.Count; i++)
            {
                string name;
                int length;
                System.Type type;

                // Get name or assign index
                name = (format[i].HasField("name")) ? format[i]["name"].str : i.ToString();

                // Get length and either store in 'length' or in 'formula'
                if (format[i]["length"].GetType().Equals(typeof(int))) {
                    length = (int)format[i]["length"].i;
                }
                else
                {
                    // Parse formula, calculate, and place into 'length'
                    length = BitConverter.ToInt32(ret[format[i]["length"].str], 0); // for now, only supporting static name. No math. 'foo' works but 'foo+bar' does not.
                }

                // Get type
                type = (format[i].HasField("type")) ? System.Type.GetType(format[i]["type"].str) : null;


                // Read bytes
                byte[] data = new byte[length];
                while (!killThreadRequested)
                {
                    readToBuffer(stream, data, length);
                }
            }

            return ret;
        }





        /// <summary>
        /// Reads readLength bytes from a network stream and saves it to buffer
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="readLength"></param>
        protected void readToBuffer(NetworkStream stream, byte[] buffer, int readLength)
        {
            int offset = 0;
            // keeps reading until a full message is received
            while (offset < buffer.Length)
            {
                int bytesRead = stream.Read(buffer, offset, readLength - offset); // read from stream
                statisticsReporter.RecordPacketReceived(bytesRead);

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



        protected void Update()
        {
            // passess all the messages that are missing
            if (messageQueue.Count > 0)
            {
                // we should not spend time processing while the queue is locked
                // as this might disconnect the socket due to timeout
                Queue<Dictionary<string, byte[]>> tmpQ;
                lock (messageQueueLock)
                {
                    // copies the queue from the thread
                    tmpQ = messageQueue;
                    messageQueue = new Queue<Dictionary<string, byte[]>>();
                }

                // now we can process our messages
                while (tmpQ.Count > 0)
                {
                    // process message received
                    Dictionary<string, byte[]> msg;
                    msg = tmpQ.Dequeue();

                    // should we drop packets?
                    while (dropAccumulatedMessages && tmpQ.Count > 0)
                    {
                        msg = tmpQ.Dequeue();
                        statisticsReporter.RecordDroppedMessage();
                    }

                    // call event handlers
                    switch (this.EventType)
                    {
                        case CommunicationMessageType.Byte:
                            ByteMessageReceived.Invoke(msg["data"]);
                            break;
                        case CommunicationMessageType.String:
                            string msgString = Encoding.UTF8.GetString(msg["data"]);
                            StringMessageReceived.Invoke(msgString);
                            break;
                        case CommunicationMessageType.Json:
                            msgString = Encoding.UTF8.GetString(msg["data"]);
                            JSONObject msgJson = new JSONObject(msgString);
                            JsonMessageReceived.Invoke(msgJson);
                            break;
                        case CommunicationMessageType.Custom:
                            CustomMessageReceived.Invoke(msg);
                            break;
                        default:
                            throw new NotImplementedException("Unimplemented message type " + this.EventType);
                    }
                }
            }
        }
    }
}
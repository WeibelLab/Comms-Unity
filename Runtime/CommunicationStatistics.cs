using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Comms
{

    /// <summary>
    /// Place this on the same GameObject as each socket connection.
    /// To get data out of this class, 
    /// create a new class which inherets it
    /// </summary>
    public class CommunicationStatistics : MonoBehaviour
    {
        // CLASS //
        protected static CommunicationStatistics ControlObject;
        protected static List<CommunicationStatistics> Collectors = new List<CommunicationStatistics>();
        protected static ulong TotalBandwidth;


        // INSTANCE //
        public string Name; // just used for identification
        public bool TCP; // just used for identification
        protected List<Message> sentMessages = new List<Message>();
        protected List<Message> receivedMessages = new List<Message>();

        // Sending
        protected ulong packets_sent;
        protected ulong bytes_sent;
        protected ulong stream_errors; // number of stream errors / disconnects
        protected DateTime connectionStarted; // time when the connection was last established
        protected List<float> connectionDurations = new List<float>();
        protected ulong stream_nreconnect;
        protected ulong bandwidth;
        /// <summary>Number of seconds to use to calculate the bandwidth</summary>
        public int bandwidthCalculatedOverTime = 10; // 10 seconds seems like a resonable average
                                                     // For Video
        protected ulong fps;
        protected uint[] resolution = new uint[2];
        // For Audio
        protected ulong sample_rate;


        protected ulong dropped_messages = 0; // happens when message was received but we were unable to process it

        // Receiving
        protected ulong packets_received;
        protected ulong bytes_received;
        protected bool isConnected;

        private void Awake()
        {
            if (!ControlObject) // First Object to start
            {
                ControlObject = this;
            }

            Collectors.Add(this);
        }

        /// <summary>
        /// Only runs on control object
        /// </summary>
        private void LateUpdate()
        {
            // Not happening on the update loop; Fix this later
            // seemed to be freezing stuff
            // Calculate Bandwidth
            //CalculateBandwidth();

            //if (ControlObject == this)
            //{
            //    CommunicationStatistics.CalculateTotalBandwidth();
            //}
        }

        #region Static Methods
        private static void CalculateTotalBandwidth()
        {
            ulong bandwidth = 0;
            foreach (CommunicationStatistics comm in CommunicationStatistics.Collectors)
            {
                bandwidth += comm.bandwidth;
            }
            CommunicationStatistics.TotalBandwidth = bandwidth;
        }
        // get inactive connections
        #endregion


        public void RecordMessageSent(int messageLength)
        {
            this.sentMessages.Add(new Message(messageLength));

            this.packets_sent++;
            this.bytes_sent += (ulong)messageLength;
        }

        public void RecordMessageSent(byte[] packet)
        {
            RecordMessageSent(packet.Length);
        }

        public void RecordMessageSent(JSONObject packet)
        {
            RecordMessageSent(packet.Print().Length);
        }

        public void RecordMessageSent(string packet)
        {
            RecordMessageSent(packet.Length);
        }

        // todo: report what error happened!
        public void RecordStreamError()
        {
            this.stream_errors++;
        }


        public void RecordDroppedMessage()
        {
            this.dropped_messages++;
        }

        public void RecordStreamDisconnect()
        {
            this.connectionDurations.Add(this.GetConnectionUpTime());
            this.isConnected = false;
        }

        public void RecordConnectionEstablished()
        {
            this.connectionStarted = DateTime.Now;
            this.isConnected = true;

            if (this.connectionDurations.Count > 0)
            {
                this.stream_nreconnect++;
            }
        }

        public void SetFPS(int fps)
        {
            this.fps = (ulong)fps;
        }

        public void SetResolution(int x, int y)
        {
            this.resolution = new uint[2] { (uint)x, (uint)y };
        }

        public void SetSampleRate(int samplesPerSeconds)
        {
            this.sample_rate = (ulong)samplesPerSeconds;
        }

        private float GetConnectionUpTime()
        {
            return (float)(DateTime.Now - this.connectionStarted).TotalSeconds;
        }

        private void CalculateBandwidth()
        {
            ulong numBytesSent = 0;
            ulong numBytesRecv = 0;
            // Sent Bytes
            for (int i = this.sentMessages.Count - 1; i >= 0; i--)
            {
                // Remove old messages
                if ((DateTime.Now - this.sentMessages[i].time).TotalSeconds > this.bandwidthCalculatedOverTime)
                {
                    this.sentMessages.RemoveAt(i);
                }
                else
                {
                    numBytesSent += this.sentMessages[i].numBytes;
                }
            }
            // Recv'd Bytes
            for (int i = this.receivedMessages.Count - 1; i >= 0; i--)
            {
                // Remove old messages
                if ((DateTime.Now - this.receivedMessages[i].time).TotalSeconds > this.bandwidthCalculatedOverTime)
                {
                    this.receivedMessages.RemoveAt(i);
                }
                else
                {
                    numBytesRecv += this.receivedMessages[i].numBytes;
                }
            }

            this.bandwidth = (numBytesSent + numBytesRecv) / (ulong)this.bandwidthCalculatedOverTime;
        }


        public void RecordMessageReceived()
        {
            // ideally this records the number of messages received
            // messages != packets
            // message could be a frame (audio or video) or a json text message
        }

        public void RecordPacketReceived(int packetSize)
        {
            this.packets_received++;
            this.bytes_received += (ulong)packetSize;

            //    this.receivedMessages.Add(new Message(packetSize));
        }

        public void RecordPacketReceived(byte[] packet)
        {
            RecordPacketReceived(packet.Length);
        }

        public void RecordPacketReceived(JSONObject packet)
        {
            RecordPacketReceived(packet.Print().Length);
        }

        public void RecordPacketReceived(string packet)
        {
            RecordPacketReceived(packet.Length);
        }
    }

    public struct Message
    {
        public ulong numBytes;
        public DateTime time;

        public Message(int numBytes)
        {
            this.numBytes = (ulong)numBytes;
            this.time = DateTime.Now;
        }
    }
}
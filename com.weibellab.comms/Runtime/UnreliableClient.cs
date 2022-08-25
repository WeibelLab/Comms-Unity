using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

namespace Comms
{
    public class UnreliableClient : Client
    {
        private UdpClient impl = new UdpClient();

        public UnreliableClient(Config config): base(config)
        {
            if (impl == null) impl = new UdpClient();
        }

        public UnreliableClient(string address, int port, Config config, string name ="UnreliableClient"): base(config)
        {
            this.config.Endpoint = new CommunicationEndpoint(address, port, name);
            // Setup Events
            OnByteMessageReceived = new CommunicationByteEvent();
            OnStringMessageReceived = new CommunicationStringEvent();
            OnJsonMessageReceived = new CommunicationJsonEvent();

            if (impl == null) impl = new UdpClient();
        }

        public UnreliableClient(System.Net.IPEndPoint endpoint, Config config, string name = "UnreliableClient"): base(config)
        {
            this.config.Endpoint = new CommunicationEndpoint(endpoint, name);
            // Setup Events
            OnByteMessageReceived = new CommunicationByteEvent();
            OnStringMessageReceived = new CommunicationStringEvent();
            OnJsonMessageReceived = new CommunicationJsonEvent();

            if (impl == null) impl = new UdpClient();
        }

        public void Configure(string address, int port, Config config, string name = "UnreliableClient")
        {
            this.config = config;
            this.config.Endpoint = new CommunicationEndpoint(address, port, name);
        }

        public void Configure(System.Net.IPEndPoint endpoint, Config config, string name = "UnreliableClient")
        {
            this.config = config;
            this.config.Endpoint = new CommunicationEndpoint(endpoint, name);
        }

        override public void Send(byte[] data)
        {
            Log($"sending message with {data.Length} bytes to {config.Endpoint.Address}:{config.Endpoint.Port}");
            //this.Header.GetHeader(); // TODO: add header
            impl.SendAsync(data, data.Length, this.config.Endpoint.AsIPEndPoint());
        }

        /// <summary>
        /// Adds a message to the runtime queue
        /// </summary>
        /// <param name="o"></param>
        override protected void DefaultMessageParser(object o)
        {
            // Check if byte[]
            if (!(o.GetType().IsArray && typeof(byte).IsAssignableFrom(o.GetType().GetElementType())))
                return;
            // Enqueue
            byte[] data = o as byte[];
            lock (MessageQueueLock)
            {
                MessageQueue.Enqueue(data);
            }
        }

        #region Unity Runtime
        #endregion
    }
}
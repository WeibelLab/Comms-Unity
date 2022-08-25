using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Comms
{
    public class UnreliableServer : Server
    {
        public int KeepClientsForSeconds;
        private Dictionary<UnreliableClient, System.DateTime> ClientTimeouts;
        private UdpClient impl;

        public UnreliableServer(Config config): base(config)
        {
            Log("Initializing Unreliable Server");
            ClientTimeouts = new Dictionary<UnreliableClient, System.DateTime>();
        }

        override public void SendTo(Client client, byte[] data)
        {
            LogWarning("Unreliable Server currently cannot send messages");
            return;
        }

        protected override void CloseSocket()
        {
            if (this.impl != null) this.impl.Close();
            for (int i=Clients.Count-1; i>=0; i--)
            {
                RunOnUnityThread(() =>
                {
                    OnClientDisconnected.Invoke(Clients[i]);
                });
                // TODO: Destroy(Clients[i]);
            }
        }

        protected override void DisposeSocket()
        {
            this.impl = null;
            this.Clients = new List<Client>();
        }

        protected override bool SetupSocket()
        {
            // Check if already running
            if (thread != null && thread.IsAlive)
            {
                LogWarning("Already running. Disable and Re-enabled component to reset");
                return false;
            }

            // Start server
            stopThread = false;
            try
            {
                this.impl = new UdpClient(config.Endpoint.AsIPEndPoint());
                return true;
            }
            catch (System.Exception err)
            {
                LogError($"Failed to initialize socket\n{err}");
                this.StopThread();
                this.impl = null;
                return false;
            }
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

        protected override void threadLoop()
        {
            IPEndPoint ipEndpoint = config.Endpoint.AsIPEndPoint();
            // TODO: statistics.RecordConnectionEstablished()
            while (!stopThread)
            {
                try
                {
                    ipEndpoint = config.Endpoint.AsIPEndPoint();
                    // Read from socket
                    byte[] msg = impl.Receive(ref ipEndpoint);
                    Log($"Received {msg.Length} bytes");
                    MessageParser(msg);
                    continue;

                    // TODO: implement header read
                    // TODO: stats.RecordPacketReceived(msg.Length);

                    // Create a client from the sender so a response can be sent
                    if (KeepClientsForSeconds != 0)
                    {
                        // Check if client already exists
                        UnreliableClient client = null;
                        for (int i=0; i<Clients.Count; i++)
                        {
                            if (Clients[i].config.Endpoint.Equals(ipEndpoint))
                            {
                                Log("Client already existed");
                                client = Clients[i] as UnreliableClient;
                                client.MessageParser(msg);
                                break;
                            }
                        }

                        // Create new client
                        if (client == null)
                        {
                            Log("Creating new Client");
                            // TODO: Unity throws warning, but this is handled through ForceUpdate
                            client = new UnreliableClient(ipEndpoint, config, $"{config.Endpoint.Name}_UDP_{Clients.Count}");
                            Clients.Add(client);
                            ClientTimeouts.Add(client, System.DateTime.Now);
                            // Events
                            client.OnByteMessageReceived.AddListener(this.OnByteMessageReceived.Invoke);
                            client.OnStringMessageReceived.AddListener(this.OnStringMessageReceived.Invoke);
                            client.OnJsonMessageReceived.AddListener(this.OnJsonMessageReceived.Invoke);

                            client.MessageParser(msg);
                        }
                    }
                    else
                    {
                        MessageParser(msg);
                    }
                }
                catch (System.Threading.ThreadAbortException)
                { }
                catch (System.Net.Sockets.SocketException socketException)
                {
                    if (socketException.SocketErrorCode != SocketError.Interrupted)
                    {
                        LogError($"Socket Error: {socketException.SocketErrorCode} -> {socketException}");
                        // TODO: stats.RecordStreamError();
                    }
                }
                catch (System.Exception err)
                {
                    LogError($"Generic Exception -> {err}");
                    // TODO: stats.RecordStreamError();
                }
            }
        }

        //private IEnumerator RemoveClientAfterTimeout(UnreliableClient client)
        //{
        //    if (KeepClientsForSeconds == -1)
        //        yield break;
            
        //    yield return new WaitForSecondsRealtime(KeepClientsForSeconds);
        //    Clients.Remove(client);
        //    //Destroy(client);
        //}

        private void CheckForTimedOutClients()
        {
            if (KeepClientsForSeconds <= 0) return; // only check if there's an actual timeout

            for (int i=Clients.Count-1; i>=0; i--)
            {
                UnreliableClient client = Clients[i] as UnreliableClient;
                if (!ClientTimeouts.ContainsKey(client))
                {
                    Clients.RemoveAt(i);
                    // TODO: Destroy(client);
                }
                else if ((System.DateTime.Now - ClientTimeouts[client]).TotalSeconds > KeepClientsForSeconds)
                {
                    ClientTimeouts.Remove(client);
                    Clients.RemoveAt(i);
                    // TODO: Destroy(client);
                }
                else
                {
                    // Hasn't timed out
                }
            }
        }


        #region Unity Runtime

        override public void Update()
        {
            if (this.impl == null) return;

            base.Update();
            ReadFromQueue();
            CheckForTimedOutClients();

            for (int i = 0; i < Clients.Count; i++)
            {
                (Clients[i] as UnreliableClient).Update();
            }
        }
        #endregion
    }
}
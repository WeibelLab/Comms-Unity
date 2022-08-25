using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Comms
{
    /// <summary>
    /// Parent class for all server entities
    /// Handles threading, typed sending types, and 
    /// </summary>
    public abstract class Server : Comms
    {
        public List<Client> Clients;

        public Server(Config config): base(config)
        {
            // Setup Thread
            this.thread = new Thread(new ThreadStart(threadLoop));
            this.stopThread = false;
            this.thread.Name = $"Unity Comms {this.config.Endpoint.Name}";

            Clients = new List<Client>();

            // TODO: create stats reporter
        }

        ~Server()
        {
            this.StopThread();
            Log($"STOPPED listening on {config.Endpoint.Address}:{config.Endpoint.Port}");
        }

        // Methods callable by other scripts
        virtual public void SendAll(byte[] data)
        {
            for (int i=0; i<this.Clients.Count; i++)
            {
                this.SendTo(Clients[i], data);
            }
        }
        virtual public void SendAll(string data)
        {
            this.SendAll(System.Text.Encoding.UTF8.GetBytes(data));
        }
        virtual public void SendAll(JSONObject data)
        {
            this.SendAll(data.ToString());

        }
        virtual public void SendTo(Client client, byte[] data)
        {
            if (this.Clients.Contains(client))
            {
                client.Send(data);
            }
            else
            {
                throw new System.Exception("Server no longer monitoring client. Try sending directly or modifying server settings");
            }
        }
        virtual public void SendTo(Client client, string data)
        {
            this.SendTo(client, System.Text.Encoding.UTF8.GetBytes(data));
        }
        virtual public void SendTo(Client client, JSONObject data)
        {
            this.SendTo(client, data.ToString());
        }

        abstract protected void threadLoop();
        abstract protected bool SetupSocket();
        abstract protected void CloseSocket();
        abstract protected void DisposeSocket();

        override public void StopThread()
        {
            if (this.thread != null)
            {
                // Asks thread to stop
                this.stopThread = true;
                CloseSocket();

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
                DisposeSocket();
            }
        }

        #region Unity Runtime
        override public void OnEnable()
        {
            base.OnEnable();

            if (this.SetupSocket())
            {
                this.thread.Start();
                Log($"STARTED listening on {config.Endpoint.Address}:{config.Endpoint.Port}");
            } else
            {
                LogError("Couldn't start server");
            }
        }

        override public void Update()
        {
            base.Update();
            ReadFromQueue();
        }
        #endregion
    }
}

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

        // Private
        private Thread ServerThread;
        protected bool stopThread;
        public int AbortTimeout = 100;

        // Methods callable by other scripts
        virtual public void SendAll(byte[] data)
        {
            for (int i=0; i<this.Clients.Count; i++)
            {
                this.Clients[i].Send(data);
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

        abstract protected void ServerThreadLoop();
        abstract protected void SetupSocket();
        abstract protected void CloseSocket();
        abstract protected void DisposeSocket();

        virtual public void StopThread()
        {
            if (this.ServerThread != null)
            {
                // Asks thread to stop
                this.stopThread = true;
                CloseSocket();

                // Aborts thread if still running
                if (this.ServerThread.IsAlive)
                {
                    this.ServerThread.Join(AbortTimeout);
                    if (this.ServerThread.IsAlive)
                        this.ServerThread.Abort();
                }

                // TODO: report statistics
                //stats.RecordStreamDisconnect();

                // Dispose
                this.ServerThread = null;
                DisposeSocket();
            }
        }

        #region Unity Runtime
        //protected void Awake()
        //{
        //    base.Awake();
        //    // TODO: Create Statstics Reporter
        //}

        private void OnEnable()
        {
            // Setup Thread
            this.ServerThread = new Thread(new ThreadStart(ServerThreadLoop));
            this.stopThread = false;

            this.SetupSocket();

            this.ServerThread.Start();
            Log($"STARTED listening on {config.Endpoint.Address}:{config.Endpoint.Port}");
        }

        private void OnDisable()
        {
            this.StopThread();
            Log($"STOPPED listening on {config.Endpoint.Address}:{config.Endpoint.Port}");
        }
        #endregion
    }
}

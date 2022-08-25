using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Comms {
    public class CommsComponent : MonoBehaviour
    {
        // Comms types and their properties
        [SerializeField]
        private int CommsTypeIndex = -1;
        public static System.Type[] CommsTypes = new System.Type[]
        {
            typeof(ReliableServer), typeof(ReliableClient),
            typeof(UnreliableServer), typeof(UnreliableClient)
        };
        public static string[] CommsNames = new string[]
        {
            "Reliable Server", "Reliable Client",
            "Unreliable Server", "Unreliable Client"
        };
        public static int[] CommsAbilities = new int[]
        { // 1 for listen, 2 for send
            1+2, // Reliable Server can listen 1 and send 2
            1+2, // Reliable Client can listen 1 and send 2
            1, // Unreliable Server can only listen 1
            2 // Unreliable Client can only send 2
        };




        [SerializeField]
        public Comms Comms; // TODO: don't discard data on editor refresh

        // Configuration
        public Config config = Config.Default;
        public bool Quiet = true;

        // Events
        public CommunicationByteEvent OnByteMessageReceived;
        public CommunicationStringEvent OnStringMessageReceived;
        public CommunicationJsonEvent OnJsonMessageReceived;
        public SocketConnectedEvent OnClientConnected;
        public SocketDisconnectedEvent OnClientDisconnected;
        public SocketErrorEvent OnClientError;

        public System.Action<object> MessageParser;

        private void OnEnable()
        {
            if (CommsTypeIndex < 0)
            {
                Debug.LogWarning("This Comms object has not been setup yet.");
                return;
            };

            // Create comms object and configure
            this.Comms = Activator.CreateInstance(CommsTypes[this.CommsTypeIndex], this.config) as Comms;

            // Copy over events
            this.Comms.OnByteMessageReceived.AddListener(this.OnByteMessageReceived.Invoke);
            this.Comms.OnStringMessageReceived.AddListener(this.OnStringMessageReceived.Invoke);
            this.Comms.OnJsonMessageReceived.AddListener(this.OnJsonMessageReceived.Invoke);

            this.Comms.OnClientConnected.AddListener(this.OnClientConnected.Invoke);
            this.Comms.OnClientDisconnected.AddListener(this.OnClientDisconnected.Invoke);
            this.Comms.OnClientError.AddListener(this.OnClientError.Invoke);

            this.Comms.OnEnable();
        }

        private void Reset()
        {
            if (CommsTypeIndex < 0 || this.Comms == null) return;
            this.Comms.Reset();
        }

        private void Start()
        {
            if (CommsTypeIndex < 0 || this.Comms == null) return;
            this.Comms.Start();
        }

        private void FixedUpdate()
        {
            if (CommsTypeIndex < 0 || this.Comms == null) return;
            this.Comms.FixedUpdate();
        }

        private void Update()
        {
            if (CommsTypeIndex < 0 || this.Comms == null) return;
            this.Comms.Update();
        }

        private void LateUpdate()
        {
            if (CommsTypeIndex < 0 || this.Comms == null) return;
            this.Comms.LateUpdate();
        }

        private void OnApplicationQuit()
        {
            if (CommsTypeIndex < 0 || this.Comms == null) return;
            this.Comms.OnApplicationQuit();
        }

        private void OnDisable()
        {
            if (CommsTypeIndex < 0 || this.Comms == null) return;
            this.Comms.OnDisable();
            this.Comms = null; // TODO: make sure is destructing
        }

        private void OnDestroy()
        {
            // Do nothing. Comms object should have already been destroyed
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.Events;

namespace Comms
{

    [System.Serializable]
    public class CommunicationJsonEvent : UnityEvent<JSONObject> { }
    [System.Serializable]
    public class CommunicationStringEvent : UnityEvent<string> { }
    [System.Serializable]
    public class CommunicationByteEvent : UnityEvent<byte[]> { }


    [System.Serializable]
    public enum CommunicationMessageType { Byte, String, Json }

    [System.Serializable]
    public enum CommunicationHeaderType { Length, Time }

    [System.Serializable]
    public struct CommunicationEndpoint
    {
        [SerializeField]
        public string Name;
        [SerializeField]
        public string Address;
        [SerializeField]
        public int Port;
        private IPEndPoint ipendpoint;

        public CommunicationEndpoint(string Address, int Port, string Name = "device")
        {
            this.Name = Name;
            this.Address = Address;
            this.Port = Port;
            this.ipendpoint = new IPEndPoint(IPAddress.Parse(this.Address), this.Port);
        }

        public void SetAddress(string address)
        {
            this.Address = address;
            this.ipendpoint.Address = IPAddress.Parse(address);
        }
        public void SetPort(int port)
        {
            this.Port = port;
            this.ipendpoint.Port = port;
        }

        public void SetName(string name)
        {
            this.Name = name;
        }
        public override string ToString()
        {
            return string.Format("\"{0}\" ({1}:{2})", Name, Address, Port);
        }

        public IPEndPoint AsIPEndPoint()
        {
            return this.ipendpoint;
        }
    }
}
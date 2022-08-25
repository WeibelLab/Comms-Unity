using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;

namespace Comms
{
    public abstract class Client : Comms
    {
        public Client(Config config) : base(config)
        {

        }

        // Sending data
        abstract public void Send(byte[] data);
        public void Send(string data)
        {
            this.Send(Encoding.UTF8.GetBytes(data));
        }
        public void Send(JSONObject data)
        {
            this.Send(data.ToString());
        }




        #region Unity Runtime
        new public void Update()
        {
            base.Update();
            ReadFromQueue();
        }
        #endregion

    }
}

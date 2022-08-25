using System.Collections;
using System.Collections.Generic;
using System;
using System.Net.Sockets;

namespace Comms
{
    public abstract class Header
    {
        public int HeaderSize { get; protected set; }
        abstract public byte[] GetHeader(byte[] msg);
        abstract public (Header, byte[]) ParseHeader(byte[] data);
        abstract public (Header, byte[]) ReadFromStream(System.Net.Sockets.NetworkStream stream);
        public static (Header, byte[]) ParseHeader(byte[] data, Type headerType)
        {
            if (!headerType.IsInstanceOfType(typeof(Header)))
            {
                throw new Exception("Passed type was not of a type derivative of the Comms.Header abstract class");
            }

            Header h = (Header)Activator.CreateInstance(headerType);
            return h.ParseHeader(data);
        }

        public static byte[] AffixHeader(byte[] header, byte[] body)
        {
            byte[] ret = new byte[header.Length + body.Length];
            Buffer.BlockCopy(header, 0, ret, 0, header.Length);
            Buffer.BlockCopy(body, 0, ret, header.Length, body.Length);
            return ret;
        }

        public byte[] AffixHeader(byte[] msg)
        {
            return Header.AffixHeader(GetHeader(msg), msg);
        }
    }

    public class NullHeader: Header
    {
        public int FixedSize = 1024;
        public NullHeader()
        {
            this.HeaderSize = 0;
        }

        public override byte[] GetHeader(byte[] msg)
        {
            return new byte[0];
        }

        public override (Header, byte[]) ParseHeader(byte[] data)
        {
            return (this, data);
        }

        public override (Header, byte[]) ReadFromStream(NetworkStream stream)
        {
            // No header to read...

            // Read body
            byte[] body = new byte[this.FixedSize];
            Comms.readToBuffer(stream, body, body.Length);

            return (this, body);
        }
    }


    public class DefaultHeader: Header
    {
        public UInt32 Length;

        public DefaultHeader()
        {
            this.HeaderSize = 4;
        }

        override public byte[] GetHeader(byte[] msg)
        {
            this.Length = (UInt32)msg.Length;
            return BitConverter.GetBytes(this.Length);
        }

        override public (Header, byte[]) ParseHeader(byte[] data)
        {
            // Check if have enough bytes
            if (data.Length < this.HeaderSize)
            {
                return (null, data);
            }

            // Parse
            this.Length = BitConverter.ToUInt32(data, 0);
            // Trim header from data
            byte[] dataWithoutHeader = new byte[data.Length - this.HeaderSize];
            Buffer.BlockCopy(data, this.HeaderSize, dataWithoutHeader, 0, dataWithoutHeader.Length);
            return (this, dataWithoutHeader);
        }

        public override (Header, byte[]) ReadFromStream(NetworkStream stream)
        {
            // Read header
            byte[] header = new byte[this.HeaderSize];
            Comms.readToBuffer(stream, header, header.Length);
            // Parse
            (Header h, byte[] x) = this.ParseHeader(header);

            // Read body
            byte[] body = new byte[this.Length];
            Comms.readToBuffer(stream, body, body.Length);

            return (this, body);
        }
    }
}
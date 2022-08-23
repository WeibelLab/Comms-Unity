using System.Collections;
using System.Collections.Generic;
using System;

namespace Comms
{
    public abstract class Header
    {
        public int HeaderSize { get; protected set; }
        abstract public byte[] GetHeader();
        abstract public (Header, byte[]) ParseHeader(byte[] data);
        public static (Header, byte[]) ParseHeader(byte[] data, Type headerType)
        {
            if (!headerType.IsInstanceOfType(typeof(Header)))
            {
                throw new Exception("Passed type was not of a type derivative of the Comms.Header abstract class");
            }

            Header h = (Header)Activator.CreateInstance(headerType);
            return h.ParseHeader(data);
        }
    }

    public class NullHeader: Header
    {
        public NullHeader()
        {
            this.HeaderSize = 0;
        }

        public override byte[] GetHeader()
        {
            return new byte[0];
        }

        public override (Header, byte[]) ParseHeader(byte[] data)
        {
            return (this, data);
        }
    }


    public class DefaultHeader: Header
    {
        public UInt32 Length;

        public DefaultHeader()
        {
            this.HeaderSize = 4;
        }

        public DefaultHeader(int Length)
        {
            this.HeaderSize = 4;
            this.Length = (UInt32)Length;
        }

        override public byte[] GetHeader()
        {
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
    }
}
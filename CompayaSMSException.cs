using System;
using System.Runtime.Serialization;

namespace Klinkby.Compaya
{
    [Serializable]
    public class CompayaSmsException : Exception
    {
        public CompayaSmsException()
        {
        }

        public CompayaSmsException(string message)
            : base(message)
        {
        }

        protected CompayaSmsException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public CompayaSmsException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
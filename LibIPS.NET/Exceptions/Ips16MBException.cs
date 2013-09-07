using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
namespace CodeIsle.LibIpsNet.Exceptions
{
    [Serializable]
    public class Ips16MBException : Exception
    {
        public Ips16MBException()
            : base() { }

        public Ips16MBException(string message)
            : base(message) { }

        public Ips16MBException(string format, params object[] args)
            : base(string.Format(format, args)) { }

        public Ips16MBException(string message, Exception innerException)
            : base(message, innerException) { }

        public Ips16MBException(string format, Exception innerException, params object[] args)
            : base(string.Format(format, args), innerException) { }

        protected Ips16MBException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}

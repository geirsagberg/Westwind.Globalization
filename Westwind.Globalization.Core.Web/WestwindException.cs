using System;

namespace Westwind.Globalization.Core.Web
{
    public class WestwindException : Exception
    {
        public WestwindException()
        {
        }

        public WestwindException(string message) : base(message)
        {
        }

        public WestwindException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
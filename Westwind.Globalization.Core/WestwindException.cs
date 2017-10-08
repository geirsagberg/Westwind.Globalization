using System;

namespace Westwind.Globalization.Core
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
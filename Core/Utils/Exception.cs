using System;

namespace Taiji.Core.Utils
{
    public sealed class ApiException : Exception
    {
        public ApiException(string message, int? code = null)
            : base(message)
        {
            Code = code;
        }

        public ApiException(string message, Exception inner)
            : base(message, inner)
        {
        }

        public int? Code { get; private set; }
    }
}

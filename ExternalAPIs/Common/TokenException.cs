using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExternalAPIs
{ 
    public class TokenException : Exception
    {
        private TokenException(string message, bool expired) : base(message) 
        { 
            IsExpired = expired;
        }
        public bool IsExpired { get; }
        internal static TokenException ForExpired()
        {
            return new TokenException("Access token has expired", true);
        }
        internal static TokenException ForInvalid()
        {
            return new TokenException("Access token is invalid or absent", false);
        }
    }
}

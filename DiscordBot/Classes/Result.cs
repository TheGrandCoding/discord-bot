using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Classes
{
    public class Result
    {
        public bool Success { get; protected set; }

        public Exception Exception { get; protected set; }
        public string ErrorMessage { get; protected set; }

        public Result()
        {
            Success = true;
        }
        public Result(Exception ex, string err = null)
        {
            Success = false;
            Exception = ex;
            ErrorMessage = err;
        }
        public Result(string err, Exception ex = null)
        {
            Success = false;
            ErrorMessage = err;
            Exception = ex;
        }
    }

    public class Result<T> : Result
    {
        public T Value { get; set; }
        public Result(T v) : base()
        {
            Value = v;
        }
        public Result(Exception ex, string err = null) : base(ex, err) { }
        public Result(string err, Exception ex = null) : base(err, ex) { }
    }
}

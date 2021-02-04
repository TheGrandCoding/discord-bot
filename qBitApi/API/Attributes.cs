using System;
using System.Collections.Generic;
using System.Text;

namespace qBitApi.API
{
    [AttributeUsage(AttributeTargets.Property)]
    internal class UnixTimestampAttribute : Attribute { }
}

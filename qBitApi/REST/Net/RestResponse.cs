using System.Collections.Generic;
using System.IO;
using System.Net;

namespace qBitApi.REST.Net
{
    public struct RestResponse
    {
        public HttpStatusCode StatusCode { get; }
        public Dictionary<string, string> Headers { get; }
        public Stream Stream { get; }

        public RestResponse(HttpStatusCode statusCode, Dictionary<string, string> headers, Stream stream)
        {
            StatusCode = statusCode;
            Headers = headers;
            Stream = stream;
        }
    }
}
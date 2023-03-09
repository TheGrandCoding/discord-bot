﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FacebookAPI
{
    public class HttpException : Exception
    {
        public readonly HttpResponseMessage _response;
        public HttpException(HttpResponseMessage response, string content) : base($"{response.StatusCode} {response.ReasonPhrase}: {content}")
        {
            _response = response;
        }

        public static async Task<HttpException> FromResponse(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            return new HttpException(response, content);
        }
    }
}
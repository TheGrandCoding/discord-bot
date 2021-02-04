using System;
using System.Collections.Generic;
using System.Text;

namespace qBitApi
{
    /// <summary> Specifies the state of the client's login status. </summary>
    public enum LoginState : byte
    {
        /// <summary> The client is currently logged out. </summary>
        LoggedOut,
        /// <summary> The client is currently logging in. </summary>
        LoggingIn,
        /// <summary> The client is currently logged in. </summary>
        LoggedIn,
        /// <summary> The client is currently logging out. </summary>
        LoggingOut
    }

    /// <summary> Specifies how a request should act in the case of an error. </summary>
    [Flags]
    public enum RetryMode
    {
        /// <summary> If a request fails, an exception is thrown immediately. </summary>
        AlwaysFail = 0x0,
        /// <summary> Retry if a request timed out. </summary>
        RetryTimeouts = 0x1,
        // /// <summary> Retry if a request failed due to a network error. </summary>
        //RetryErrors = 0x2,
        /// <summary> Retry if a request failed due to a rate-limit. </summary>
        RetryRatelimit = 0x4,
        /// <summary> Retry if a request failed due to an HTTP error 502. </summary>
        Retry502 = 0x8,
        /// <summary> Continuously retry a request until it times out, its cancel token is triggered, or the server responds with a non-502 error. </summary>
        AlwaysRetry = RetryTimeouts | /*RetryErrors |*/ RetryRatelimit | Retry502,
    }
}

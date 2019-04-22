using System;
using System.Net.Http.Headers;

namespace SirTalksALotBrain
{
    public interface IJWTToken
    {
        bool IsExpired { get; }
        DateTime Issued { get; }

        void AddToHeader(HttpRequestHeaders httpRequestHeaders);
    }
}
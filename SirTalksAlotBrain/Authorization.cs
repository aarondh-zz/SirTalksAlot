using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace SirTalksALotBrain
{
    public class Authorization
    {
        private class JWTToken : IJWTToken
        {
            public DateTime Issued { get; private set; }
            private string _rawToken;
            private Dictionary<string, string> _properties;
            public int ExpiresIn
            {
                get
                {
                    return int.Parse(_properties["expires_in"]);
                }
            }
            public int ExtExpiresIn
            {
                get
                {
                    return int.Parse(_properties["ext_expires_in"]);
                }
            }
            public string TokenType
            {
                get
                {
                    return _properties["token_type"];
                }
            }
            public string AccessToken
            {
                get
                {
                    return _properties["access_token"];
                }
            }
            public JWTToken(string rawToken)
            {
                Issued = DateTime.Now;
                _rawToken = rawToken;
                _properties = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(_rawToken);
            }
            public bool IsExpired
            {
                get
                {
                    if (_properties != null)
                    {
                        return DateTime.Now > Issued.AddSeconds(ExpiresIn);
                    }
                    return true;
                }
            }
            public void AddToHeader(HttpRequestHeaders httpRequestHeaders)
            {
                httpRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            }

        }

        public static async Task<IJWTToken> Authenticate(string microsoftAppId, string microsoftAppPassword)
        {
            if (string.IsNullOrEmpty(microsoftAppId))
            {
                throw new ArgumentException("Invalid or missing", nameof(microsoftAppId));
            }

            if (string.IsNullOrEmpty(microsoftAppPassword))
            {
                throw new ArgumentException("Invalid or missing", nameof(microsoftAppPassword));
            }

            var authenticationUri = new Uri("https://login.microsoftonline.com/botframework.com/oauth2/v2.0/token");

            using (var client = new HttpClient())
            {
                var parameters = new Dictionary<string, string>();
                parameters.Add("grant_type", "client_credentials");
                parameters.Add("client_id", microsoftAppId);
                parameters.Add("client_secret", microsoftAppPassword);
                parameters.Add("scope", "https://api.botframework.com/.default");
                var content = new FormUrlEncodedContent(parameters);
                var response = await client.PostAsync(authenticationUri, content);
                if (response.IsSuccessStatusCode)
                {
                    var accessToken = await response.Content.ReadAsStringAsync();
                    return new JWTToken(accessToken);
                }
                else
                {
                    return null;
                }
            }
        }

    }
}

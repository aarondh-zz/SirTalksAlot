using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Primitives;
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.Bot.Connector;
using System.Text;

namespace SirTalksALotBrain
{
    public static class BotMessage
    {
        [FunctionName("BotMessage")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            try
            {
                if (!HasValidAuthHeader(req.Headers))
                {
                    return req.CreateResponse(HttpStatusCode.Forbidden, "Forbidden");
                }
                else
                {
                    var message = await req.Content.ReadAsAsync<Activity>();
                    if (message == null)
                    {
                        throw new InvalidOperationException("Not a valid bot Activity message");
                    }
                    if (message.Type == ActivityTypes.Message)
                    {
                        var microsoftAppId = Environment.GetEnvironmentVariable("MicrosoftAppId");
                        var microsoftAppPassword = Environment.GetEnvironmentVariable("MicrosoftAppPassword");
                        var botId = Environment.GetEnvironmentVariable("BotId");
                        var accessToken = await Authorization.Authenticate(microsoftAppId, microsoftAppPassword);
                        Activity reply;
                        if (message.Text == "HEADERS")
                        {
                            var response = new StringBuilder();
                            foreach (var item in req.Headers)
                            {
                                response.AppendFormat("{0}: {1}", item.Key, String.Join(", ", item.Value));
                            }
                            reply = message.CreateReply(response.ToString());
                        }
                        else
                        {
                            reply = message.CreateReply($"You sent {message.Text.Length} characters");
                        }

                        var result = await ReplyToActivityAsync(message.ServiceUrl, accessToken, reply, message.Id);

                        return req.CreateResponse(HttpStatusCode.OK);
                    }
                    else
                    {
                        var reply = HandleSystemMessage(message);
                        if (reply != null)
                        {
                            var microsoftAppId = Environment.GetEnvironmentVariable("MicrosoftAppId");
                            var microsoftAppPassword = Environment.GetEnvironmentVariable("MicrosoftAppPassword");
                            var accessToken = await Authorization.Authenticate(microsoftAppId, microsoftAppPassword);

                            var result = await ReplyToActivityAsync(message.ServiceUrl, accessToken, reply);
                        }
                        return req.CreateResponse(HttpStatusCode.OK);
                    }
                }
            }
            catch( Exception e)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, e.Message + e.StackTrace);
            }
        }
        private static async Task<string> ReplyToActivityAsync(string serviceUrl, IJWTToken accessToken, Activity responseMessage, string activityId = null)
        {
            Uri botServiceUri = new Uri(serviceUrl);
            string uriPattern = activityId == null ? "/v3/conversations/{conversationId}/activities" : "/v3/conversations/{conversationId}/activities/{activityId}";
            UriTemplate botResponseUriTemplate = new UriTemplate(uriPattern);
            using (var client = new HttpClient())
            {
                NameValueCollection parameters = new NameValueCollection();
                parameters.Add("conversationId", responseMessage.Conversation.Id);
                parameters.Add("activityId", responseMessage.ReplyToId);

                Uri botResponseUri = botResponseUriTemplate.BindByName(botServiceUri, parameters);

                var jsonText = JsonConvert.SerializeObject(responseMessage);
                var botResponseContent = new StringContent(jsonText, Encoding.UTF8, "application/json");
                botResponseContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json; charset=utf-8");
                HttpRequestMessage request = null;
                HttpResponseMessage response = null;
                try
                {
                    request = new HttpRequestMessage
                    {
                        RequestUri = botResponseUri,
                        Content = botResponseContent,
                        Method = HttpMethod.Post
                    };

                    accessToken.AddToHeader(request.Headers);

                    response = await client.SendAsync(request).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        throw new InvalidOperationException(response.StatusCode.ToString());
                    }
                }
                finally
                {
                    request.Dispose();
                    if (response != null)
                    {
                        response.Dispose();
                    }
                }
            }
        }

        private static bool HasValidAuthHeader(HttpRequestHeaders headers)
        {
            IEnumerable<string> authorizations;
            if( headers.TryGetValues("Authorization", out authorizations) )
            {
                var actualAuthHeader = authorizations.First();
                var appId = Environment.GetEnvironmentVariable("MicrosoftAppId") ?? "YourAppId";
                var appSecret = Environment.GetEnvironmentVariable("MicrosoftAppPassword") ?? "YourAppSecret";
                var expectedAuthHeader = "Basic " +
                    System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(appId + ":" + appSecret));

                return actualAuthHeader == expectedAuthHeader;
            }
#if DEBUG
            return true;
#else
            return false;
#endif
        }

        private static Activity HandleSystemMessage(Activity message)
        {
            if (message.Type == "BotAddedToConversation")
            {
                return message.CreateReply($"Welcome {message.From?.Name}!");
            }
            else if (message.Type == "BotRemovedFromConversation")
            {
                return message.CreateReply($"Bye {message.From?.Name}!");
            }

            return null;
        }
    }
}
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
            if (!HasValidAuthHeader(req.Headers))
            {
                return req.CreateResponse(HttpStatusCode.Forbidden, "Forbidden");
            }
            var message = await req.Content.ReadAsAsync<Activity>();

            if (message.Type == ActivityTypes.Message)
            {
                var microsoftAppId = Environment.GetEnvironmentVariable("MicrosoftAppId");
                var microsoftAppPassword = Environment.GetEnvironmentVariable("MicrosoftAppPassword");
                var botId = Environment.GetEnvironmentVariable("BotId");
                var accessToken = await Authorization.Authenticate(microsoftAppId, microsoftAppPassword);

                var reply = message.CreateReply($"You sent {message.Text.Length} characters");

                var result = await ReplyToActivityAsync(message.ServiceUrl, accessToken, reply);

                return req.CreateResponse(HttpStatusCode.OK);
            }
            else
            {
                HandleSystemMessage(message);
                return req.CreateResponse(HttpStatusCode.OK);
            }
        }
        private static async Task<string> ReplyToActivityAsync(string serviceUrl, IJWTToken accessToken, Activity responseMessage)
        {
            Uri botServiceUri = new Uri(serviceUrl);
            UriTemplate botResponseUriTemplate = new UriTemplate("/v3/conversations/{conversationId}/activities/{activityId}");
            using (var client = new HttpClient())
            {
                NameValueCollection parameters = new NameValueCollection();
                parameters.Add("conversationId", responseMessage.Conversation.Id);
                parameters.Add("activityId", responseMessage.ReplyToId);

                Uri botResponseUri = botResponseUriTemplate.BindByName(botServiceUri, parameters);

                var jsonText = JsonConvert.SerializeObject(responseMessage);
                var botResponseContent = new StringContent(jsonText, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage { RequestUri = botResponseUri,
                                                       Content = botResponseContent,
                                                       Method = HttpMethod.Post};

                accessToken.AddToHeader(request.Headers);

                var response = await client.SendAsync(request);
                if ( response.IsSuccessStatusCode )
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    throw new InvalidOperationException(response.StatusCode.ToString());
                }
            }
        }

        private static bool HasValidAuthHeader(HttpRequestHeaders headers)
        {
#if DEBUG
            return true;
#else
            var actualAuthHeader = headers.GetValues("Authorization").First();
            var appId = Environment.GetEnvironmentVariable("AppId") ?? "YourAppId";
            var appSecret = Environment.GetEnvironmentVariable("AppSecret") ?? "YourAppSecret";
            var expectedAuthHeader = "Basic " +
                System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(appId + ":" + appSecret));

            return actualAuthHeader == expectedAuthHeader;
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
using System;
using System.IO;
using System.Text;

using Newtonsoft.Json;

using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace OnCreatePlayer
{
    public class Function
    {
        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("OnCreatePlayer Lambda hit. Request:");
            Console.Write(JsonConvert.SerializeObject(request));

            IAmazonDynamoDB _ddbClient = new AmazonDynamoDBClient();

            try
            {
                var connectionId = request.RequestContext.ConnectionId;

                context.Logger.LogLine($"ConnectionId: {connectionId}");

                var newPlayer = MapRequestToPlayer(request);

                var ddbRequest = new PutItemRequest
                {
                    TableName = "DraftSnake_Players",
                    Item = new Dictionary<string, AttributeValue>
                    {
                        { "DraftId", new AttributeValue{ S = newPlayer.DraftId }},
                        { "Name", new AttributeValue{S = newPlayer.Name} },
                        { "ConnectionId", new AttributeValue{ S = newPlayer.ConnectionId }},
                        { "IsConnected", new AttributeValue{ BOOL = newPlayer.IsConnected } }
                    }
                };

                await _ddbClient.PutItemAsync(ddbRequest);

                return new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = "Socket Connected."
                };
            }
            catch (Exception e)
            {
                context.Logger.LogLine("Error connecting: " + e.Message);
                context.Logger.LogLine(e.StackTrace);
                return new APIGatewayProxyResponse
                {
                    StatusCode = 500,
                    Body = $"Failed to connect: {e.Message}"
                };
            }
        }
        public Player MapRequestToPlayer(APIGatewayProxyRequest request)
        {
            var body = JsonConvert.DeserializeObject<JObject>(request.Body);
            var draftId = body["draftId"]?.ToString();
            var name = body["newPlayer"]?["name"]?.ToString();

            

            // TODO: Validate draft is active and Name is not a duplicate

            return new Player()
            {
                DraftId = draftId,
                Name = name,
                ConnectionId = request.RequestContext.ConnectionId,
                IsConnected = true
            };
        }

        public class Player
        {
            public string DraftId { get; set; }
            public string Name { get; set; }
            public string ConnectionId { get; set; }
            public Boolean IsConnected { get; set; }
        }
    }
}
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
using Amazon.SQS.Model;
using Amazon.SQS;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace OnCreatePlayer
{
    public class Function
    {
        IAmazonDynamoDB _ddbClient = new AmazonDynamoDBClient();
        IAmazonSimpleSystemsManagement _paramClient = new AmazonSimpleSystemsManagementClient();

        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("OnCreatePlayer Lambda hit. Request:");
            Console.Write(JsonConvert.SerializeObject(request));

            try
            {

                var newPlayer = MapRequestToPlayer(request);

                var existingPlayers = await RetrievePlayers(newPlayer.DraftId);

                // TODO: Validate player name unique, num of Players limit, game in signup stage etc

                await AddPlayerToDb(newPlayer);

                existingPlayers.Add(newPlayer);
                
                // Send message to queue
                await SendMessageToQueue(newPlayer, existingPlayers);

                return new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = "Player created successfully."
                };
            }
            catch (Exception e)
            {
                context.Logger.LogLine("Error connecting: " + e.Message);
                context.Logger.LogLine(e.StackTrace);
                return new APIGatewayProxyResponse
                {
                    StatusCode = 500,
                    Body = $"Failed to create player. Error message: {e.Message}"
                };
            }
        }

        public Player MapRequestToPlayer(APIGatewayProxyRequest request)
        {
            Console.WriteLine($"Mapping request");

            var body = JsonConvert.DeserializeObject<JObject>(request.Body);
            var draftId = body["draftId"]?.ToString();
            var name = body["newPlayer"]?["name"]?.ToString();

            return new Player()
            {
                DraftId = draftId,
                Name = name,
                ConnectionId = request.RequestContext.ConnectionId,
                IsConnected = true
            };
        }

        private async Task<List<Player>> RetrievePlayers(string draftId)
        {
            Console.WriteLine($"Retrieving players for draft {draftId}.");

            var request = new QueryRequest
            {
                TableName = "Players",
                KeyConditionExpression = "DraftId = :v_DraftId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                    {":v_DraftId", new AttributeValue { S = draftId }},
                },
            };

            var scanResponse = await _ddbClient.QueryAsync(request);

            var players = new List<Player>();

            scanResponse.Items.ForEach(i =>
            {
                players.Add(
                    new Player()
                        {
                            DraftId = i["DraftId"].S,
                            Name = i["Name"].S,
                            ConnectionId = i["ConnectionId"].S,
                            IsConnected = true
                        }
                 );

            });

            return players;
        }

        private async Task AddPlayerToDb(Player newPlayer)
        {

            Console.WriteLine($"Adding player {newPlayer.Name} to Db");

            var ddbRequest = new PutItemRequest
            {
                TableName = "Players",
                Item = new Dictionary<string, AttributeValue>
                    {
                        { "DraftId", new AttributeValue{ S = newPlayer.DraftId }},
                        { "Name", new AttributeValue{S = newPlayer.Name} },
                        { "ConnectionId", new AttributeValue{ S = newPlayer.ConnectionId }},
                        { "IsConnected", new AttributeValue{ BOOL = newPlayer.IsConnected } }
                    }
            };

            await _ddbClient.PutItemAsync(ddbRequest);
        }

        private async Task<string> RetrieveParam(string paramToRetrieve)
        {
            Console.WriteLine($"Retrieving {paramToRetrieve} param.");

            var paramRequest = new GetParametersRequest
            {
                Names = new List<string>() {paramToRetrieve}
            };

            var response = await _paramClient.GetParametersAsync(paramRequest);

            return response.Parameters[0].Value;
        }

        public async Task SendMessageToQueue(Player newPlayer, List<Player> players)
        {

            var sqsConfig = new AmazonSQSConfig();

            sqsConfig.ServiceURL = "https://sqs.us-east-1.amazonaws.com";

            var sqsClient = new AmazonSQSClient(sqsConfig);

            var eventMessage = new PlayerCreatedEvent()
            {
                EventType = EventType.PlayerCreated,
                DraftId = newPlayer.DraftId,
                NewPlayerName = newPlayer.Name,
                Players = players
            };

            var messageRequest = new SendMessageRequest();

            var queueUrl = await RetrieveParam("WEB_SOCKET_EVENTS_URL");

            Console.WriteLine($"Sending message to {queueUrl} queue.");

            messageRequest.QueueUrl = queueUrl;

            messageRequest.MessageBody = JsonConvert.SerializeObject(eventMessage);

            await sqsClient.SendMessageAsync(messageRequest);

            Console.WriteLine("The following message was sent to queue:");
            Console.Write(messageRequest.MessageBody);
        }

        public class Player
        {
            public string DraftId { get; set; }
            public string Name { get; set; }
            public string ConnectionId { get; set; }
            public Boolean IsConnected { get; set; }
        }

        public class PlayerCreatedEvent
        {
            public EventType EventType { get; set; }
            public string DraftId { get; set; }
            public string NewPlayerName { get; set; }
            public List<Player> Players { get; set; }
        }

        public enum EventType
        {
            PlayerCreated = 1,
            PickSubmitted = 1
        }
    }
}
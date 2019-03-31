using System;
using System.IO;
using System.Text;

using Newtonsoft.Json;

using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.DynamoDBv2.Model;
using System.Threading.Tasks;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Amazon.Runtime;
using System.Net;
using System.Linq;
using Amazon.Lambda.APIGatewayEvents;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace DraftSnake_Players_SendMessage
{
    public class Function
    {
        IAmazonDynamoDB _ddbClient = new AmazonDynamoDBClient();
        private static readonly JsonSerializer _jsonSerializer = new JsonSerializer();

        public async Task FunctionHandler(DynamoDBEvent dynamoEvent, ILambdaContext context)
        {
            context.Logger.LogLine($"Beginning to process {dynamoEvent.Records.Count} records...");

            foreach (var record in dynamoEvent.Records)
            {
                context.Logger.LogLine($"Event ID: {record.EventID}");
                context.Logger.LogLine($"Event Name: {record.EventName}");

                string streamRecordJson = JsonConvert.SerializeObject(record.Dynamodb);
                context.Logger.LogLine($"Processing DynamoDB Record:");
                context.Logger.LogLine(streamRecordJson);

                if (record.EventName == "INSERT")
                {
                    await ProcessInsertEventAsync(record, context);
                }

                else
                {
                    context.Logger.LogLine("Event is not INSERT. Moving on.");
                }

            }

            context.Logger.LogLine("Stream processing complete.");
        }

        private async Task ProcessInsertEventAsync(DynamoDBEvent.DynamodbStreamRecord record, ILambdaContext context)
        {
            context.Logger.LogLine($"Processing {record.EventName} event");

            var newPlayer = MapDynamodbItemToPlayer(record.Dynamodb.NewImage);

            var draftId = record.Dynamodb.Keys["DraftId"].S;

            var players = await RetrievePlayersForDraft(draftId);

            var playerEvent = new PlayerEvent(PlayerEventType.PlayerAdded, players, newPlayer);

            var playerEventString = JsonConvert.SerializeObject(playerEvent);

            await SendMessageToPlayers(players, playerEventString);

            await Task.CompletedTask;
        }

        private async Task SendMessageToPlayers(List<Player> players, string message)
        {
            Console.WriteLine("Sending Message To Players");

            var endpoint = System.Environment.GetEnvironmentVariable("ENDPOINT");
            Console.WriteLine($"Endpoint url is {endpoint}");

            var apiClient = new AmazonApiGatewayManagementApiClient(new AmazonApiGatewayManagementApiConfig
            {
                ServiceURL = endpoint
            });

            foreach (var player in players.Where(x => x.IsConnected))
            {
                var connectionId = player.ConnectionId;

                var postConnectionRequest = new PostToConnectionRequest
                {
                    ConnectionId = connectionId,
                    Data = new MemoryStream(UTF8Encoding.UTF8.GetBytes(message))
                };

                try
                {
                    Console.WriteLine($"Post to connection {connectionId}");
                    await apiClient.PostToConnectionAsync(postConnectionRequest);
                }
                catch (AmazonServiceException e)
                {
                    // API Gateway returns a status of 410 GONE when the connection is no
                    // longer available. If this happens, we simply delete the identifier
                    // from our DynamoDB table.
                    if (e.StatusCode == HttpStatusCode.Gone)
                    {
                        //Dictionary<string, AttributeValueUpdate> updates = new Dictionary<string, AttributeValueUpdate>();
                        //// Update item's Setting attribute
                        //updates["IsConnected"] = new AttributeValueUpdate()
                        //{
                        //    Action = AttributeAction.PUT,
                        //    Value = new AttributeValue { BOOL = false }
                        //};

                        //var ddbUpdateRequest = new UpdateItemRequest
                        //{
                        //    TableName = "DraftSnake_Players",
                        //    Key = new Dictionary<string, AttributeValue>
                        //    {
                        //        {"ConnectionId", new AttributeValue {S = connectionId}}
                        //    },
                        //    AttributeUpdates = updates
                        //};

                        Console.WriteLine($"setting gone connection: {connectionId} to inactive");
                        //await _ddbClient.UpdateItemAsync(ddbUpdateRequest);
                    }
                    else
                    {
                        Console.WriteLine($"Error posting message to {connectionId}: {e.Message}");
                        Console.WriteLine(e.StackTrace);
                    }
                }
            }
        }

        private async Task<List<Player>> RetrievePlayersForDraft(string draftId)
        {
            Console.WriteLine("RetrievingPlayersForDraft");

            var request = new QueryRequest
            {
                TableName = "DraftSnake_Players",
                KeyConditionExpression = "DraftId = :v_DraftId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                    {":v_DraftId", new AttributeValue { S = draftId }},
                },
            };

            var scanResponse = await _ddbClient.QueryAsync(request);

            Console.Write("Query Reponse", JsonConvert.SerializeObject(scanResponse.Items));

            var players = new List<Player>();
            scanResponse.Items.ForEach(x => players.Add(MapDynamodbItemToPlayer(x)));

            return players;
        }

        public Player MapDynamodbItemToPlayer(Dictionary<string, AttributeValue> item)
        {
            Console.WriteLine("Mapping DynamoDb Item To Players");

            return new Player()
            {
                DraftId = item["DraftId"].S,
                Name = item["Name"].S,                
                ConnectionId = item["ConnectionId"].S,
                IsConnected = item["IsConnected"].BOOL,
            };
        }

        public class PlayerEvent
        {
            public PlayerEventType EventType { get; set; }
            public List<Player> Players { get; set; }
            public Player NewPlayer { get; set; }

            public PlayerEvent(PlayerEventType eventType, List<Player> players, Player newPlayer)
            {
                EventType = eventType;
                Players = players;
                NewPlayer = newPlayer;
            }
        }

        public enum PlayerEventType
        {
            PlayerAdded,
            OtherEvent
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
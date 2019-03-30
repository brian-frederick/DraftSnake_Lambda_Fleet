using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.Runtime;
using Newtonsoft.Json;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace SendMessage
{
    public class Function
    {
        IAmazonDynamoDB _ddbClient = new AmazonDynamoDBClient();

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {

        }


        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
        /// to respond to SQS messages.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
        {
            foreach(var message in evnt.Records)
            {
                await ProcessMessageAsync(message, context);
            }
        }

        private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            context.Logger.LogLine($"Processing message {message.Body}");

            var draftId = message.Attributes["DraftId"];

            var playerConnections = await RetrievePlayerConnections(draftId);

            await SendMessageToPlayers(playerConnections, message.Body);

            // TODO: Do interesting work based on the new message
            await Task.CompletedTask;
        }

        private async Task SendMessageToPlayers(List<Dictionary<string, AttributeValue>> playerConnections, string message)
        {
            
            var endpoint = System.Environment.GetEnvironmentVariable("ENDPOINT");
            Console.WriteLine($"Endpoint url is {endpoint}");

            var apiClient = new AmazonApiGatewayManagementApiClient(new AmazonApiGatewayManagementApiConfig
            {
                ServiceURL = endpoint
            });

            foreach (var player in playerConnections)
            {
                var connectionId = player["ConnectionId"].S;

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
                        Dictionary<string, AttributeValueUpdate> updates = new Dictionary<string, AttributeValueUpdate>();
                        // Update item's Setting attribute
                        updates["IsConnected"] = new AttributeValueUpdate()
                        {
                            Action = AttributeAction.PUT,
                            Value = new AttributeValue { BOOL = false }
                        };

                        var ddbUpdateRequest = new UpdateItemRequest
                        {
                            TableName = "DraftSnake_Players",
                            Key = new Dictionary<string, AttributeValue>
                            {
                                {"ConnectionId", new AttributeValue {S = connectionId}}
                            },
                            AttributeUpdates = updates 
                        };

                        Console.WriteLine($"setting gone connection: {connectionId} to inactive");
                        await _ddbClient.UpdateItemAsync(ddbUpdateRequest);
                    }
                    else
                    {
                        Console.WriteLine($"Error posting message to {connectionId}: {e.Message}");
                        Console.WriteLine(e.StackTrace);
                    }
                }
            }
        }

        private async Task<List<Dictionary<string, AttributeValue>>> RetrievePlayerConnections(string draftId) {

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

            return scanResponse.Items;
        }
    }
}

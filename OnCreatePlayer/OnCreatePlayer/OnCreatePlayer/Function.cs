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

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace OnCreatePlayer
{
    public class Function
    {
        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine( "OnCreatePlayer Lambda hit");

            IAmazonDynamoDB _ddbClient = new AmazonDynamoDBClient();

            try
            {
                var connectionId = request.RequestContext.ConnectionId;

                context.Logger.LogLine($"ConnectionId: {connectionId}");

                var ddbRequest = new PutItemRequest
                {
                    TableName = "DraftSnake_Players",
                    Item = new Dictionary<string, AttributeValue>
                    {
                        { "ConnectionId", new AttributeValue{ S = connectionId }},
                        { "DraftId", new AttributeValue{ S = "a" }},
                        { "IsConnected", new AttributeValue{ BOOL = true } }
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


    }
}
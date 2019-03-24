using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Newtonsoft.Json;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace OnConnect
{
    public class Function
    {
        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            Console.WriteLine("OnConnect Lambda hit");

            Console.Write($"request.body: {JsonConvert.SerializeObject(request.Body)}");

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
                        { "PlayerId", new AttributeValue{ S = connectionId}},
                        { "DraftId", new AttributeValue{ S = "a"}},
                        { "IsSocketActive", new AttributeValue{ BOOL = true} }
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

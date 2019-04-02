using System;
using System.IO;
using System.Text;

using Newtonsoft.Json;

using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.DynamoDBv2.Model;
using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.DynamoDBv2;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace OnSubmitPick
{
    public class Function
    {
        private static readonly JsonSerializer _jsonSerializer = new JsonSerializer();
        IAmazonDynamoDB _ddbClient = new AmazonDynamoDBClient();


        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("OnSubmitPick Lambda hit. Request:");
            Console.Write(JsonConvert.SerializeObject(request));

            var newPick = MapRequestBodyToPick(request.Body);

            try
            {
                newPick.OverallOrder = await RetrieveNextOverallOrder(newPick.DraftId);

                await SubmitPickToDb(newPick);

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

        public Pick MapRequestBodyToPick(string requestBody)
        {
            var body = JsonConvert.DeserializeObject<JObject>(requestBody);
            var draftId = body["draftId"]?.ToString();
            var playerName = body["name"]?.ToString();
            var selection = body["pick"]?["selection"]?.ToString();

            return new Pick()
            {
                DraftId = draftId,
                PlayerId = playerName,
                Selection = selection
            };
        }

        public async Task<int> RetrieveNextOverallOrder(string draftId)
        {
            // If we don't find any picks for this draft, our first pick will be OverallOrder 1.
            var nextOverallOrder = 1;

            var ddbQueryRequest = new QueryRequest
            {
                TableName = "DraftSnake_Picks",
                KeyConditionExpression = "DraftId = :v_DraftId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> {
                        {":v_DraftId", new AttributeValue { S = draftId }},
                    },
                ScanIndexForward = false,
                Limit = 1
            };

            var queryResponse = await _ddbClient.QueryAsync(ddbQueryRequest);

            if (queryResponse?.Items.Count > 0)
            {
                Console.WriteLine($"Current highest OverallOrder {queryResponse.Items[0]["OverallOrder"]}");

                // With ScanIndexForward as false, the highest OverallOrder will be the first item in the list.
                int.TryParse(queryResponse.Items[0]["OverallOrder"].N, out nextOverallOrder);
                nextOverallOrder++;
            }

            return nextOverallOrder;
        }

        public async Task SubmitPickToDb(Pick newPick)
        {
            var ddbRequest = new PutItemRequest
            {
                TableName = "DraftSnake_Picks",
                Item = new Dictionary<string, AttributeValue>
                    {
                        { "DraftId", new AttributeValue{ S = newPick.DraftId }},
                        { "OverallOrder", new AttributeValue{N = newPick.OverallOrder.ToString() } },
                        { "PlayerId", new AttributeValue{ S = newPick.PlayerId }},
                        { "Selection", new AttributeValue{ S = newPick.Selection } }
                    },
                
            };

            await _ddbClient.PutItemAsync(ddbRequest);
        }

        public class Pick
        {
            public string DraftId { get; set; }
            public int OverallOrder { get; set; }
            public string PlayerId { get; set; }
            public string Selection { get; set; }
        }
    }
}
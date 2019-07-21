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
using Microsoft.Extensions.DependencyInjection;
using DraftSnakeLibrary.Services.Picks;
using DraftSnakeLibrary.Services.Messages;
using DraftSnakeLibrary.Repositories;
using DraftSnakeLibrary.Models.Picks;
using DraftSnakeLibrary.Services;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace OnSubmitPick
{
    public class Function
    {
        private static readonly JsonSerializer _jsonSerializer = new JsonSerializer();
        private readonly IPickService _pickService;

        public Function()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();
            _pickService = serviceProvider.GetRequiredService<IPickService>();
            //_messageService = serviceProvider.GetRequiredService<IMessageService<PlayerCreatedMessage>>();
        }

        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("OnSubmitPick Lambda hit. Request:");
            Console.Write(JsonConvert.SerializeObject(request));

            var newPick = MapRequestBodyToPick(request.Body);

            try
            {
                await _pickService.Put(newPick);

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

        public void ConfigureServices(ServiceCollection services)
        {
            services.AddTransient<IPickService, PickService>();
            //services.AddTransient<IMessageService<>, MessageService<>>();
            services.AddTransient<IModelDynamoDbRepository<Pick>, PickRepository>();
            services.AddTransient<IAmazonDynamoDB, AmazonDynamoDBClient>();
            services.AddTransient<IModelMapper<Pick>, PickMapper>();
            //services.AddTransient<IMessageRepository<PlayerCreatedMessage>, MessageRepository<PlayerCreatedMessage>>();
            //services.AddTransient<IAmazonSQS, AmazonSQSClient>();
        }

    }
}
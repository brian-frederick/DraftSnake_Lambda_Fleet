using System;
using Newtonsoft.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using DraftSnakeLibrary.Services.Players;
using DraftSnakeLibrary.Services.Configurations;
using DraftSnakeLibrary.Models.Players;
using DraftSnakeLibrary.Services.Messages;
using DraftSnakeLibrary.Models.Messages;
using Microsoft.Extensions.DependencyInjection;
using DraftSnakeLibrary.Repositories;
using Amazon.DynamoDBv2;
using DraftSnakeLibrary.Services;
using Amazon.SQS;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace OnCreatePlayer
{
    public class Function
    {
        private readonly IPlayerService _playerService;
        private readonly IMessageService<PlayerCreatedMessage> _messageService;

        public Function()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();
            _playerService = serviceProvider.GetRequiredService<IPlayerService>();
            _messageService = serviceProvider.GetRequiredService<IMessageService<PlayerCreatedMessage>>();
        }

        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {           
            context.Logger.LogLine("OnCreatePlayer Lambda hit. Request:");
            Console.Write(JsonConvert.SerializeObject(request));

            var newPlayer = MapRequestToPlayer(request);

            try
            {
                await _playerService.Put(newPlayer);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error creating player. Error: {e.Message}");

                return new APIGatewayProxyResponse
                {
                    StatusCode = 500,
                    Body = $"There was an error creating Player: {newPlayer.Name}."
                };
            }

            try
            {
                await _messageService.SendMessage(new PlayerCreatedMessage(newPlayer));
            }
            catch (Exception e)
            {

                Console.WriteLine($"Error sending PlayerCreatedMessage. Error: {e.Message}");

                // there will be so many messages sent when players are being created that we'll get the new set of players on the next event.
                return new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = $"There was an error sending message to other players but it looks like our new player was created."
                };
            }


            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = $"Player {newPlayer.Name} created."
            };

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


        public void ConfigureServices(ServiceCollection services)
        {
            services.AddTransient<IPlayerService, PlayerService>();
            services.AddTransient<IMessageService<PlayerCreatedMessage>, MessageService<PlayerCreatedMessage>>();
            services.AddTransient<IModelDynamoDbRepository<Player>, PlayerRepository>();
            services.AddTransient<IAmazonDynamoDB, AmazonDynamoDBClient>();
            services.AddTransient<IModelMapper<Player>, PlayerMapper>();
            services.AddTransient<IMessageRepository<PlayerCreatedMessage>, MessageRepository<PlayerCreatedMessage>>();
            services.AddTransient<IAmazonSQS, AmazonSQSClient>();
        }

    }
}
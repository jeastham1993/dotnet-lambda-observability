using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using ObservableLambda.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;
using ObservableLambda.Shared.Messaging;
using ObservableLambda.Shared.Model;
using ObservableLambda.Shared.ViewModel;
using OpenTelemetry.Trace;
using TraceOptions = ObservableLambda.Shared.TraceOptions;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ObservableLambda
{
    public class Function : ObservableFunction<APIGatewayProxyRequest, APIGatewayProxyResponse>
    {
        private static string TABLE_NAME = Environment.GetEnvironmentVariable("TABLE_NAME") ?? "ObservableLambda";
        
        private readonly IAmazonSimpleNotificationService _snsClient;
        private readonly IAmazonDynamoDB _dynamoDbClient;

        public Function() : base(new TraceOptions("ObservableLambda.Api")
        {
            AddAwsInstrumentation = false
        })
        {
            this._snsClient = new AmazonSimpleNotificationServiceClient(new EnvironmentVariablesAWSCredentials());
            this._dynamoDbClient = new AmazonDynamoDBClient(new EnvironmentVariablesAWSCredentials());
        }

        internal Function(IAmazonSimpleNotificationService snsClient, IAmazonDynamoDB dynamoDb, TraceOptions options) : base(options)
        {
            this._snsClient = snsClient;
            this._dynamoDbClient = dynamoDb;
        }

        public override Func<APIGatewayProxyRequest, ILambdaContext, Task<APIGatewayProxyResponse>> Handler =>
            FunctionHandler;

        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request,
            ILambdaContext context)
        {
            if (string.IsNullOrEmpty(request.Body))
            {
                return new APIGatewayProxyResponse()
                {
                    StatusCode = 400,
                    Body = "{\"message\": \"Invalid Request Body\"}",
                    Headers = new Dictionary<string, string>(2)
                    {
                        {"Content-Type", "application/json"},
                        {"traceparent", Activity.Current.TraceId.ToString()}
                    }
                };
            }

            UserDTO user;

            using (var serialization = Activity.Current.Source.StartActivity("deserialize-body"))
            {
                serialization.AddTag("request.length", request.Body.Length);
                
                user = JsonSerializer.Deserialize<UserDTO>(request.Body);
            }

            var userToCreate = User.Create(user.EmailAddress);

            using (var dynamoPutItemActivity = Activity.Current.Source.StartActivity("dynamo-db-put-item"))
            {
                var dynamoResponse = await this._dynamoDbClient.PutItemAsync(new PutItemRequest()
                {
                    TableName = TABLE_NAME,
                    Item =new Dictionary<string, AttributeValue>(2)
                    {
                        {"PK", new AttributeValue(userToCreate.UserId)},
                        {"EmailAddress", new AttributeValue(userToCreate.EmailAddress)}
                    },
                    ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
                });

                dynamoPutItemActivity?.AddTag("dynamodb.read_capacity_units", dynamoResponse.ConsumedCapacity.ReadCapacityUnits.ToString());
                dynamoPutItemActivity?.AddTag("dynamodb.write_capacity_units", dynamoResponse.ConsumedCapacity.WriteCapacityUnits.ToString());
                dynamoPutItemActivity?.AddTag("dynamodb.table_name", TABLE_NAME);
                dynamoPutItemActivity?.AddTag("dynamodb.partition", userToCreate.UserId);
            }

            using (var publishActivity = Activity.Current.Source.StartActivity("sns-publish"))
            {
                var publishResult = await this._snsClient.PublishAsync(new PublishRequest()
                {
                    TopicArn = Environment.GetEnvironmentVariable("TOPIC_ARN"),
                    Message = new MessageWrapper<User>(userToCreate).ToString(),
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>(2)
                    {
                        {"traceparent", new MessageAttributeValue(){StringValue = Activity.Current.TraceId.ToString(), DataType = "String"}},
                        {"parentspan", new MessageAttributeValue(){StringValue = Activity.Current.SpanId.ToString(), DataType = "String"}},
                        
                    }
                });

                publishActivity?.AddTag("sns.result", ((int)publishResult.HttpStatusCode).ToString());
                publishActivity?.AddTag("sns.published_message_id", publishResult.MessageId);
                publishActivity?.AddTag("sns.published_message_seq_number", publishResult.SequenceNumber);
                publishActivity?.AddTag("sns.published_message_length", publishResult.ContentLength);
            }

            return new APIGatewayProxyResponse()
            {
                StatusCode = 200,
                Body = "Hello Validator!",
                Headers = new Dictionary<string, string>(2)
                {
                    {"Content-Type", "application/json"},
                    {"traceparent", Activity.Current.TraceId.ToString()}
                }
            };
        }
    }
}
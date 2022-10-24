using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using ObservableLambda.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;
using ObservableLambda.Shared.Messaging;
using TraceOptions = ObservableLambda.Shared.TraceOptions;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ObservableLambda
{
    public class Function : ObservableFunction<APIGatewayProxyRequest, APIGatewayProxyResponse>
    {
        private readonly IAmazonSimpleNotificationService _snsClient;

        public Function() : base(new TraceOptions("ObservableLambda.Api")
        {
            AddAwsInstrumentation = false
        })
        {
            this._snsClient = new AmazonSimpleNotificationServiceClient(new EnvironmentVariablesAWSCredentials());
        }

        internal Function(IAmazonSimpleNotificationService snsClient, TraceOptions options) : base(options)
        {
            this._snsClient = snsClient;
        }

        public override Func<APIGatewayProxyRequest, ILambdaContext, Task<APIGatewayProxyResponse>> Handler =>
            FunctionHandler;

        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request,
            ILambdaContext context)
        {
            var counter = this.Meter.CreateCounter<int>("RequestCounter");
            this.Logger.LogInformation("New request received");

            using (var publishActivity = Activity.Current.Source.StartActivity("sns-publish"))
            {
                var publishResult = await this._snsClient.PublishAsync(new PublishRequest()
                {
                    TopicArn = Environment.GetEnvironmentVariable("TOPIC_ARN"),
                    Message = new MessageWrapper<string>("Hello").ToString(),
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

            this.Logger.LogInformation("Successfully described DynamoDB table");

            counter.Add(1);

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
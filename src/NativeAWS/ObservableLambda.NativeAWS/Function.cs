using System;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using ObservableLambda.NativeAWS.Shared;
using Amazon.DynamoDBv2;
using System.Threading.Tasks;
using System.Collections.Generic;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ObservableLambda.NativeAWS
{

    public class Function : ObservableFunction
    {
        public Function() : base() { }

        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            Logging.AddTraceToLogContext(Tracing.CurrentTraceId);

            await this.Notify(new RequestReceived());

            var dynamoClient = new AmazonDynamoDBClient();
            _ = await dynamoClient.DescribeTableAsync(Environment.GetEnvironmentVariable("TABLE_NAME"));

            Logging.Info("Successfully described DynamoDB table");

            return new APIGatewayProxyResponse()
            {
                StatusCode = 200,
                Body = "Hello Validator!",
                Headers = new Dictionary<string, string>
                {
                    {"Content-Type", "text/plain"},
                }
            };
        }
    }
}
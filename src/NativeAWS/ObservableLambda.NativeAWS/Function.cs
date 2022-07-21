using System;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using ObservableLambda.NativeAWS.Shared;
using Amazon.DynamoDBv2;
using System.Threading.Tasks;
using System.Collections.Generic;
using Amazon.XRay.Recorder.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ObservableLambda.NativeAWS
{

    public class Function : ObservableFunction
    {
        public Function() : base() {
            AWSSDKHandler.RegisterXRayForAllServices();
        }

        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            Logging.AddTraceToLogContext(AWSXRayRecorder.Instance.GetEntity().TraceId);

            await this.Notify(new RequestReceived());

            var dynamoClient = new AmazonDynamoDBClient();
            _ = await dynamoClient.DescribeTableAsync(Environment.GetEnvironmentVariable("TABLE_NAME"));

            Logging.Info("Successfully described DynamoDB table");

            AWSXRayRecorder.Instance.BeginSubsegment("DelayedWork");

            await Task.Delay(3000);

            AWSXRayRecorder.Instance.EndSubsegment();

            Logging.Info("Tracing ended");

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
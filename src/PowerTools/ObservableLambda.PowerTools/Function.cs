using System;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.DynamoDBv2;
using System.Threading.Tasks;
using System.Collections.Generic;
using AWS.Lambda.Powertools.Logging;
using AWS.Lambda.Powertools.Metrics;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using AWS.Lambda.Powertools.Tracing;
using Amazon.Lambda.Serialization.SystemTextJson;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace ObservableLambda.PowerTools
{
    public class Function
    {
        private readonly AmazonDynamoDBClient _dynamoClient;

        public Function() : this(null)
        {
        }
        
        internal Function(AmazonDynamoDBClient dynamoClient)
        {
            AWSSDKHandler.RegisterXRayForAllServices();

            this._dynamoClient = dynamoClient ?? new AmazonDynamoDBClient();
        }

        [Metrics(CaptureColdStart = true)]
        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            try
            {
                var requestContextRequestId = request.RequestContext.RequestId;

                var lookupInfo = new Dictionary<string, object>()
                {
                    {"LookupInfo", new Dictionary<string, object>{{ "LookupId", requestContextRequestId }}}
                };

                Logger.AppendKeys(lookupInfo);

                Logger.LogInformation("Describing table from DynamoDB");

                _ = await this._dynamoClient.DescribeTableAsync(Environment.GetEnvironmentVariable("TABLE_NAME"));

                Tracing.WithSubsegment("Tracing Subcall",
                    subsegment =>
                    {
                        subsegment.AddAnnotation("AccountId", request.RequestContext.AccountId);
                        subsegment.AddMetadata("LookupInfo", request.RequestContext.RequestId);
                    });

                await Task.Delay(3000);

                Metrics.AddDimension("Environment","Prod");
                Metrics.AddMetric("SuccessfulBooking", 1, MetricUnit.Count);

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
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failure describing DynamoDB table");

                return new APIGatewayProxyResponse()
                {
                    StatusCode = 400,
                    Headers = new Dictionary<string, string>
                    {
                        {"Content-Type", "text/plain"},
                    }
                };
            }
        }
    }
}
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using ObservableLambda.Shared;
using OpenTelemetry.Contrib.Instrumentation.AWSLambda.Implementation;
using System;
using Amazon.DynamoDBv2;
using Microsoft.Extensions.Logging;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ObservableLambda
{

    public class Function
    {
        private ILogger<Function> _logger;
        public Function()
        {
            Logging<Function>.Init();
            Tracing.Init();
            Metrics.Init();

            this._logger = Logging<Function>.Logger;
        }
        
        public APIGatewayProxyResponse TracingFunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            return AWSLambdaWrapper.Trace(Tracing.TraceProvider, FunctionHandler, request, context);
        }

        /// <summary>
        /// A simple function that takes a APIGatewayProxyRequest and returns a APIGatewayProxyResponse
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public APIGatewayProxyResponse FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var counter = Metrics.CreateCounter("RequestCounter");
            this._logger.LogInformation("New request received");
            
            var dynamoClient = new AmazonDynamoDBClient();
            _ = dynamoClient.DescribeTableAsync(Environment.GetEnvironmentVariable("TABLE_NAME")).Result;
            
            this._logger.LogInformation("Successfully described DynamoDB table");
            
            counter.Add(1);
            
            return new APIGatewayProxyResponse() { StatusCode = 200, Body = "Hello Validator!" };
        }
    }
}
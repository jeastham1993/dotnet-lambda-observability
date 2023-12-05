// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.

using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
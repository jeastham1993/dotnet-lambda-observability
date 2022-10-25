using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.Runtime;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Microsoft.Extensions.Logging;
using ObservableLambda.Shared;
using ObservableLambda.Shared.Messaging;
using ObservableLambda.Shared.Model;
using ObservableLambda.Shared.ViewModel;
using OpenTelemetry.Trace;
using TraceOptions = ObservableLambda.Shared.TraceOptions;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ObservableLambda.Processor.SingleMessage
{
    public class Function : ObservableFunction<SQSEvent, string>
    {
        private readonly IAmazonStepFunctions _stepFunctionsClient;
        
        public Function() : base(new TraceOptions("ObservableLambda.Processor.Batch")
        {
            AddAwsInstrumentation = false,
            AutoStartTrace = false
        })
        {
            this._stepFunctionsClient = new AmazonStepFunctionsClient(new EnvironmentVariablesAWSCredentials());
        }

        internal Function(IAmazonStepFunctions stepFunctions, TraceOptions options) : base(options)
        {
            this._stepFunctionsClient = stepFunctions;
        }

        public override Func<SQSEvent, ILambdaContext, Task<string>> Handler =>
            FunctionHandler;

        public async Task<string> FunctionHandler(SQSEvent request,
            ILambdaContext lambdaContext)
        {
            foreach (var message in request.Records)
            {
                var context = this.HydrateContextFromSnsMessage(message);
                
                using (var rootSpan = this.Source.StartActivity(lambdaContext.FunctionName, ActivityKind.Server, parentContext: context))
                {
                    rootSpan.AddTag("messaging.message_id", message.MessageId);

                    await this.processMessage(message);
                }
            }

            return "OK";
        }

        private async Task processMessage(SQSEvent.SQSMessage message)
        {
            try
            {
                var messageContents = parseMessage(message);

                using (var userWorkflowActivity = Activity.Current.Source.StartActivity("step-functions-start-workflow"))
                {
                    // Logic here to kick off a user flow for step functions
                    var startExecutionResponse = await this._stepFunctionsClient.StartExecutionAsync(new StartExecutionRequest()
                    {
                        Input = JsonSerializer.Serialize(messageContents),
                        StateMachineArn = Environment.GetEnvironmentVariable("STATE_MACHINE_ARN"),
                    });

                    userWorkflowActivity.AddTag("states.state_machine_arn", Environment.GetEnvironmentVariable("STATE_MACHINE_ARN"));
                    userWorkflowActivity.AddTag("states.execution_arn", startExecutionResponse.ExecutionArn);
                }
            }
            catch (Exception e)
            {
                Activity.Current.RecordException(e);
                
                throw;
            }
        }
        
        private ActivityContext HydrateContextFromSnsMessage(SQSEvent.SQSMessage message)
        {
            var wrappedMessage = parseMessage(message);
       
            var hydratedContext = new ActivityContext(ActivityTraceId.CreateFromString(wrappedMessage.Metadata.TraceParent.AsSpan()),
                ActivitySpanId.CreateFromString(wrappedMessage.Metadata.ParentSpan.AsSpan()), ActivityTraceFlags.Recorded);

            return hydratedContext;
        }

        private MessageWrapper<UserDTO> parseMessage(SQSEvent.SQSMessage message)
        {
            var snsData = JsonSerializer.Deserialize<SnsToSqsMessageBody>(message.Body);
            return JsonSerializer.Deserialize<MessageWrapper<UserDTO>>(snsData.Message);
        }
    }
}
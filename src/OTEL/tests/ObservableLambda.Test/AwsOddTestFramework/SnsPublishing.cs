using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Amazon.Lambda.SQSEvents;
using Amazon.Lambda.TestUtilities;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Moq;
using ObservableLambda.Processor.Batch;
using TraceOptions = ObservableLambda.Shared.TraceOptions;

namespace ObservableLambda.Test.AwsOddTestFramework;

public class SnsPublishing
{
    public Mock<IAmazonSimpleNotificationService> MockSnsClient;

    public List<PublishRequest> RawMessages;

    public SnsPublishing()
    {
        RawMessages = new List<PublishRequest>();
        
        MockSnsClient = new Mock<IAmazonSimpleNotificationService>();
        MockSnsClient.Setup(p => p.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PublishRequest, CancellationToken>((data, token) =>
            {
                RawMessages.Add(data);
            })
            .ReturnsAsync(new PublishResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                MessageId = "test-message-id",
                SequenceNumber = "001",
                ContentLength = 150,
            });
    }

    public Task[] ShouldInvokeViaSqs(List<LambdaInvokeProps> handlers, List<Activity> activities)
    {
        var sqsEvent = new SQSEvent() {Records = new List<SQSEvent.SQSMessage>(RawMessages.Count)};

        foreach (var message in RawMessages)
        {
            sqsEvent.Records.Add(new SQSEvent.SQSMessage()
            {
                Body = JsonSerializer.Serialize(new SnsToSqsMessageBody(){Message = message.Message, TopicArn = message.TopicArn})
            });
        }
        var traceOptions = new TraceOptions()
        {
            CollectedSpans = activities,
            ServiceName = "ObservableLambda.UnitTest",
            AddAwsInstrumentation = false,
            AddLambdaConfiguration = false
        };

        var tasks = new List<Task>();

        foreach (var handler in handlers)
        {
            var components = handler.Handler.Split("::");
            var assembly = components[0];
            var classPath = components[1];
            var methodName = components[2];
            
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

            var loadedAssembly = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(assembly));
            var type = loadedAssembly.GetType(classPath);
            var instance = Activator.CreateInstance(type, flags, null, handler.ConstructorParameters, null);
            var method = type.GetMethod(methodName);

            var result = (Task)method.Invoke(instance, new object?[2]{sqsEvent, new TestLambdaContext()});

            tasks.Add(result);
        }

        return tasks.ToArray();
    }
}
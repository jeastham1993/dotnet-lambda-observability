using System.Diagnostics;
using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Moq;
using ObservableLambda.Shared;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TraceOptions = ObservableLambda.Shared.TraceOptions;

namespace ObservableLambda.Test;

public class TraceTests
{
    [Fact]
    public async Task TestTracing()
    {
        var activities = new List<Activity>();

        var mockSnsClient = new Mock<IAmazonSimpleNotificationService>();
        mockSnsClient.Setup(p => p.PublishAsync(It.IsAny<PublishRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishResponse()
            {
                HttpStatusCode = HttpStatusCode.OK,
                MessageId = "test-message-id",
                SequenceNumber = "001",
                ContentLength = 150,
            });
        
        var function = new Function(new TraceOptions()
        {
            CollectedSpans = activities,
            ServiceName = "ObservableLambda.UnitTest"
        }, mockSnsClient.Object);

        var functionResult = await function.TracedFunctionHandler(new APIGatewayProxyRequest(), new TestLambdaContext());

        var snsPublishActivity = activities.FirstOrDefault(p => p.DisplayName.Contains("sns-publish"));
        
        Assert.NotNull(snsPublishActivity);

        var resultTag = snsPublishActivity.Tags.FirstOrDefault(p => p.Key == "sns.result");

        Assert.Equal("200", resultTag.Value);
    }
}
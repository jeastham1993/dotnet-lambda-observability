namespace Serverless.OpenTelemetry;

using Amazon.Runtime;
using Amazon.Util;

using AwsSignatureVersion4.Private;

public class SignedRequestHandler : DelegatingHandler
{
    private static readonly KeyValuePair<string, IEnumerable<string>>[] EmptyRequestHeaders =
        Array.Empty<KeyValuePair<string, IEnumerable<string>>>();

    public SignedRequestHandler()
    {
        this.InnerHandler = new HttpClientHandler();
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RemoveHeaders(request);

        var credentials = new ImmutableCredentials(
            Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"),
            Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"),
            Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN"));

        await Signer.SignAsync(
            request,
            null,
            null,
            DateTime.Now,
            "eu-west-1",
            "osis",
            credentials);

        return await base.SendAsync(
                request,
                cancellationToken)
            .ConfigureAwait(false);
    }

    protected override HttpResponseMessage Send(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RemoveHeaders(request);

        var credentials = new ImmutableCredentials(
            Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"),
            Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"),
            Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN"));

        Signer.Sign(
            request,
            null,
            null,
            DateTime.Now,
            "eu-west-1",
            "osis",
            credentials);

        return base.Send(
            request,
            cancellationToken);
    }

    /// <summary>
    /// Given the idempotent nature of message handlers, lets remove request headers that
    /// might have been added by an prior attempt to send the request.
    /// </summary>
    private static void RemoveHeaders(HttpRequestMessage request)
    {
        request.Headers.Remove(HeaderKeys.AuthorizationHeader);
        request.Headers.Remove(HeaderKeys.XAmzContentSha256Header);
        request.Headers.Remove(HeaderKeys.XAmzDateHeader);
        request.Headers.Remove(HeaderKeys.XAmzSecurityTokenHeader);
    }
}
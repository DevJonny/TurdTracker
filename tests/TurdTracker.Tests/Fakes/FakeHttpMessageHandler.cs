using System.Net;

namespace TurdTracker.Tests.Fakes;

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(Func<HttpRequestMessage, bool> Matcher, HttpResponseMessage Response)> _responses = [];
    private HttpResponseMessage _defaultResponse = new(HttpStatusCode.NotFound);

    public List<HttpRequestMessage> SentRequests { get; } = [];

    /// <summary>Adds a response for requests matching the given URL substring.</summary>
    public FakeHttpMessageHandler RespondTo(string urlPattern, HttpResponseMessage response)
    {
        _responses.Add((req => req.RequestUri?.ToString().Contains(urlPattern) == true, response));
        return this;
    }

    /// <summary>Adds a response for requests matching a custom predicate.</summary>
    public FakeHttpMessageHandler RespondTo(Func<HttpRequestMessage, bool> matcher, HttpResponseMessage response)
    {
        _responses.Add((matcher, response));
        return this;
    }

    /// <summary>Sets the default response when no matcher matches.</summary>
    public FakeHttpMessageHandler WithDefault(HttpResponseMessage response)
    {
        _defaultResponse = response;
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SentRequests.Add(request);

        foreach (var (matcher, response) in _responses)
        {
            if (matcher(request))
            {
                return Task.FromResult(response);
            }
        }

        return Task.FromResult(_defaultResponse);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.ServiceDiscovery.Abstractions;

namespace Microsoft.Extensions.ServiceDiscovery.Http;

/// <summary>
/// <see cref="HttpClientHandler"/> which resolves endpoints using service discovery.
/// </summary>
public class ResolvingHttpClientHandler : HttpClientHandler
{
    private readonly HttpServiceEndPointResolver _resolver;
    private readonly TimeProvider _timeProvider;

    internal ResolvingHttpClientHandler(HttpServiceEndPointResolver resolver, TimeProvider timeProvider)
    {
        _resolver = resolver;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var originalUri = request.RequestUri;
        IEndPointHealthFeature? epHealth = null;
        Exception? error = null;
        var startTimestamp = _timeProvider.GetTimestamp();
        if (originalUri?.Host is not null)
        {
            var result = await _resolver.GetEndpointAsync(request, cancellationToken).ConfigureAwait(false);
            request.RequestUri = ResolvingHttpDelegatingHandler.GetUriWithEndPoint(originalUri, result);
            request.Headers.Host ??= result.Features.Get<IHostNameFeature>()?.HostName;
            epHealth = result.Features.Get<IEndPointHealthFeature>();
        }

        try
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            error = exception;
            throw;
        }
        finally
        {
            var elapsed = _timeProvider.GetElapsedTime(startTimestamp);
            epHealth?.ReportHealth(elapsed, error); // Report health so that the resolver pipeline can take health and performance into consideration, possibly triggering a circuit breaker?.
            request.RequestUri = originalUri;
        }
    }
}

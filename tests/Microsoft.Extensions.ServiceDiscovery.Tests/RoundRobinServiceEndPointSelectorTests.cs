// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.ServiceDiscovery.Abstractions;
using Xunit;

namespace Microsoft.Extensions.ServiceDiscovery.Tests;

/// <summary>
/// Tests for <see cref="RoundRobinServiceEndPointSelector"/>.
/// </summary>
public class RoundRobinServiceEndPointSelectorTests
{
    [Fact]
    public void StartsAtFirstEndPoint()
    {
        var first = ServiceEndPoint.Create(new IPEndPoint(IPAddress.Loopback, 1243));
        var second = ServiceEndPoint.Create(new DnsEndPoint("localhost", 1243));
        var selector = new RoundRobinServiceEndPointSelector();
        var collection = new ServiceEndPointCollection(
            serviceName: "service",
            endpoints: [first, second],
            new CancellationChangeToken(CancellationToken.None),
            new FeatureCollection());
        selector.SetEndPoints(collection);
        Assert.Same(first, selector.GetEndPoint(null));
    }
}

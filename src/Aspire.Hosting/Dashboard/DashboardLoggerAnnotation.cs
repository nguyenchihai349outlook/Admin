// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Channels;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Dashboard;
/// <summary>
/// Represents an annotation for logging messages to the dashboard.
/// </summary>
public class DashboardLoggerAnnotation : IResourceAnnotation, ILogger
{
    /// <summary>
    /// 
    /// </summary>
    public List<(string, bool)> Backlog { get; } = [];
    /// <summary>
    /// Gets the channel for logging messages to the dashboard.
    /// </summary>
    public Channel<(string, bool)> LogStream { get; } = Channel.CreateUnbounded<(string, bool)>();

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    /// <param name="state"></param>
    /// <returns></returns>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="logLevel"></param>
    /// <returns></returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    /// <param name="logLevel"></param>
    /// <param name="eventId"></param>
    /// <param name="state"></param>
    /// <param name="exception"></param>
    /// <param name="formatter"></param>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        LogStream.Writer.TryWrite((formatter(state, exception), logLevel >= LogLevel.Error));
    }
}

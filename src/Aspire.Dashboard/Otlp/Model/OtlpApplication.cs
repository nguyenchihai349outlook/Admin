// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Diagnostics;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;

namespace Aspire.Dashboard.Otlp.Model;

[DebuggerDisplay("ApplicationName = {ApplicationName}, InstanceId = {InstanceId}")]
public class OtlpApplication
{
    public const string SERVICE_NAME = "service.name";
    public const string SERVICE_INSTANCE_ID = "service.instance.id";

    public string ApplicationName { get; }
    public string InstanceId { get; }
    public int Suffix { get; }

    private readonly ConcurrentDictionary<string, OtlpMeter> _meters = new();
    private readonly ConcurrentDictionary<(string MeterName, string InstrumentName), OtlpInstrument> _instruments = new();

    private readonly ILogger _logger;

    public KeyValuePair<string, string>[] Properties { get; }

    public OtlpApplication(Resource resource, IReadOnlyDictionary<string, OtlpApplication> applications, ILogger logger)
    {
        var properties = new List<KeyValuePair<string, string>>();
        foreach (var attribute in resource.Attributes)
        {
            switch (attribute.Key)
            {
                case SERVICE_NAME:
                    ApplicationName = attribute.Value.GetString();
                    break;
                case SERVICE_INSTANCE_ID:
                    InstanceId = attribute.Value.GetString();
                    break;
                default:
                    properties.Add(new KeyValuePair<string, string>(attribute.Key, attribute.Value.GetString()));
                    break;

            }
        }
        Properties = properties.ToArray();
        if (string.IsNullOrEmpty(ApplicationName))
        {
            ApplicationName = "Unknown";
        }
        if (string.IsNullOrEmpty(InstanceId))
        {
            //
            // NOTE: The service.instance.id value is a recommended attribute, but not required.
            //       See: https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/semantic_conventions/README.md#service-experimental
            //
            InstanceId = ApplicationName;
        }
        Suffix = applications.Where(a => a.Value.ApplicationName == ApplicationName).Count();
        _logger = logger;
    }

    public Dictionary<string, string> AllProperties()
    {
        var props = new Dictionary<string, string>();
        props.Add(SERVICE_NAME, ApplicationName);
        props.Add(SERVICE_INSTANCE_ID, InstanceId);

        foreach (var kv in Properties)
        {
            props.TryAdd(kv.Key, kv.Value);
        }

        return props;
    }

    public string UniqueApplicationName => $"{ApplicationName}-{Suffix}";

    public void AddMetrics(AddContext context, RepeatedField<ScopeMetrics> scopeMetrics)
    {
        foreach (var sm in scopeMetrics)
        {
            foreach (var metric in sm.Metrics)
            {
                try
                {
                    if (!_instruments.TryGetValue((metric.Name, sm.Scope.Name), out var instrument))
                    {
                        instrument = GetInstrumentSlow(metric, sm.Scope);
                    }

                    instrument.AddInstrumentValuesFromGrpc(metric);
                }
                catch (Exception ex)
                {
                    context.FailureCount++;
                    _logger.LogInformation(ex, "Error adding metric.");
                }
            }
        }

        OtlpInstrument GetInstrumentSlow(Metric metric, InstrumentationScope scope)
        {
            return _instruments.GetOrAdd((metric.Name, scope.Name), key =>
            {
                return new OtlpInstrument(metric, GetMeter(scope));
            });
        }
    }

    private OtlpMeter GetMeter(InstrumentationScope scope)
    {
        return _meters.GetOrAdd(scope.Name, static (name, scope) => new OtlpMeter(scope), scope);
    }

    public List<OtlpInstrument> GetInstruments()
    {
        return _instruments.Values.ToList();
    }
}

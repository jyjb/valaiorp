namespace Valaiorp.Observability.Metrics
{
    using System.Collections.Concurrent;
    using System.Diagnostics.Metrics;
    using Valaiorp.Core.Contracts;

    public sealed class MetricsCollector : IDisposable
    {
        private readonly Meter _meter;
        private readonly Counter<long> _executionCounter;
        private readonly Counter<long> _failureCounter;
        private readonly Histogram<double> _executionDuration;
        private readonly ConcurrentDictionary<string, Counter<long>> _customCounters = new();

        public MetricsCollector(string meterName = "Valaiorp.Metrics")
        {
            _meter = new Meter(meterName);
            _executionCounter = _meter.CreateCounter<long>("execution.count", "Execution Count");
            _failureCounter = _meter.CreateCounter<long>("execution.failures", "Execution Failures");
            _executionDuration = _meter.CreateHistogram<double>("execution.duration.ms", "Execution Duration (ms)");
        }

        public void RecordExecutionStart(string operationName)
        {
            _executionCounter.Add(1, new KeyValuePair<string, object?>("operation", operationName));
        }

        public void RecordExecutionEnd(string operationName, TimeSpan duration, bool isSuccess)
        {
            _executionDuration.Record(duration.TotalMilliseconds, new KeyValuePair<string, object?>("operation", operationName));
            if (!isSuccess)
            {
                _failureCounter.Add(1, new KeyValuePair<string, object?>("operation", operationName));
            }
        }

        public void RecordCustomMetric(string metricName, long value, string? dimension = null)
        {
            var counter = _customCounters.GetOrAdd(metricName, name =>
            {
                return _meter.CreateCounter<long>(name, $"{name} Count");
            });

            counter.Add(value, dimension != null ? new KeyValuePair<string, object?>("dimension", dimension) : default);
        }

        public void Dispose()
        {
            _meter.Dispose();
        }
    }
}
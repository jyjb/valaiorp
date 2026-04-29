namespace Valaiorp.Runtime.Bot
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Observability.Contracts;

    /// <summary>
    /// Point-in-time health snapshot returned by <see cref="BotWorker.GetHealthAsync"/>.
    /// </summary>
    public sealed record BotWorkerHealth(
        bool            IsRunning,
        int             PendingCount,
        int             InProgressCount,
        int             DeadLetterCount,
        DateTimeOffset? LastHeartbeatUtc);

    /// <summary>
    /// Continuously running bot that pulls IWorkItems from a shared IWorkQueue and
    /// executes each one through AgentRuntime.
    ///
    /// Deploy N BotWorkers across N machines all pointing at the same IWorkQueue
    /// backend (SqlWorkQueue for multi-machine, InMemoryWorkQueue for single-process).
    ///
    /// Lifecycle:
    ///   StartAsync()  — registers a run, begins the poll loop (non-blocking)
    ///   StopAsync()   — drains in-flight work up to drainTimeout, ends the run, then stops
    ///   DisposeAsync() — stop + dispose the underlying AgentRuntime
    ///
    /// Retry / dead-letter (at queue level, outer layer above tool-level retry):
    ///   On success               → MarkCompletedAsync
    ///   On failure + retry left  → MarkFailedAsync (item re-queued automatically)
    ///   On failure + max reached → MarkFailedAsync (item dead-lettered automatically)
    /// </summary>
    public sealed class BotWorker : IAsyncDisposable
    {
        private readonly AgentRuntime                        _runtime;
        private readonly IWorkQueue                          _queue;
        private readonly IBotContext                         _botContext;
        private readonly string                              _queueId;
        private readonly Func<IWorkItem, IExecutionContext>  _contextFactory;
        private readonly int                                 _maxConcurrency;
        private readonly int                                 _maxAttempts;
        private readonly TimeSpan                            _pollInterval;
        private readonly ILogger?                            _logger;

        private CancellationTokenSource? _cts;
        private Task?                    _loopTask;
        private string?                  _runId;
        private DateTimeOffset           _lastHeartbeatUtc;
        private int                      _inFlightCount;

        public BotWorker(
            AgentRuntime                       runtime,
            IWorkQueue                         queue,
            IBotContext                        botContext,
            string                             queueId,
            Func<IWorkItem, IExecutionContext> contextFactory,
            int      maxConcurrency = 4,
            int      maxAttempts    = 3,
            TimeSpan pollInterval   = default,
            ILogger? logger         = null)
        {
            _runtime        = runtime;
            _queue          = queue;
            _botContext     = botContext;
            _queueId        = queueId;
            _contextFactory = contextFactory;
            _maxConcurrency = maxConcurrency;
            _maxAttempts    = maxAttempts;
            _pollInterval   = pollInterval <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(500) : pollInterval;
            _logger         = logger;
        }

        public IBotContext BotContext => _botContext;
        public bool        IsRunning  => _loopTask is { IsCompleted: false };

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        /// <summary>Registers a queue run and starts the poll loop (non-blocking).</summary>
        public async Task StartAsync(CancellationToken externalCt = default)
        {
            if (IsRunning) return;
            var run = await _queue.StartRunAsync(_queueId, _botContext.BotId, externalCt).ConfigureAwait(false);
            _runId    = run.RunId;
            _cts      = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
            _loopTask = RunLoopAsync(_cts.Token);

            if (_logger != null)
                await _logger.LogAsync(LogLevel.Information,
                    $"[BotWorker] Started: bot={_botContext.BotId}, queue={_queueId}, run={_runId}",
                    ct: externalCt).ConfigureAwait(false);
        }

        /// <summary>
        /// Signals stop, waits up to <paramref name="drainTimeout"/> for in-flight items to
        /// complete, then ends the queue run. Defaults to 30 seconds. Logs a warning if the
        /// drain window is exceeded so that stuck tasks are visible in production.
        /// </summary>
        public async Task StopAsync(TimeSpan drainTimeout = default)
        {
            if (_cts != null) await _cts.CancelAsync().ConfigureAwait(false);

            if (_loopTask != null)
            {
                var timeout = drainTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : drainTimeout;
                using var drainCts = new CancellationTokenSource(timeout);
                try
                {
                    await _loopTask.WaitAsync(drainCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (_logger != null)
                        await _logger.LogAsync(LogLevel.Warning,
                            $"[BotWorker] Drain timeout ({timeout}) exceeded; " +
                            $"{_inFlightCount} task(s) still in flight — bot={_botContext.BotId}",
                            ct: CancellationToken.None).ConfigureAwait(false);
                }
            }

            if (_runId != null) await _queue.EndRunAsync(_runId).ConfigureAwait(false);

            if (_logger != null)
                await _logger.LogAsync(LogLevel.Information,
                    $"[BotWorker] Stopped: bot={_botContext.BotId}, queue={_queueId}, run={_runId}",
                    ct: CancellationToken.None).ConfigureAwait(false);
        }

        // ── Health ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a point-in-time health snapshot, including queue depths and the last
        /// heartbeat timestamp. The heartbeat advances once per poll iteration; a stale
        /// timestamp indicates the loop has stalled.
        /// </summary>
        public async Task<BotWorkerHealth> GetHealthAsync(CancellationToken ct = default)
        {
            var pending    = await _queue.GetPendingCountAsync(_queueId, ct).ConfigureAwait(false);
            var inProgress = await _queue.GetInProgressCountAsync(_queueId, ct).ConfigureAwait(false);
            var deadLetter = await _queue.GetDeadLetterCountAsync(_queueId, ct).ConfigureAwait(false);
            return new BotWorkerHealth(
                IsRunning,
                pending,
                inProgress,
                deadLetter,
                _lastHeartbeatUtc == default ? null : _lastHeartbeatUtc);
        }

        // ── Monitoring ────────────────────────────────────────────────────────────

        public Task<int> GetPendingCountAsync(CancellationToken ct = default)
            => _queue.GetPendingCountAsync(_queueId, ct);

        public Task<int> GetInProgressCountAsync(CancellationToken ct = default)
            => _queue.GetInProgressCountAsync(_queueId, ct);

        public Task<int> GetDeadLetterCountAsync(CancellationToken ct = default)
            => _queue.GetDeadLetterCountAsync(_queueId, ct);

        public Task<QueueReport> GetReportAsync(CancellationToken ct = default)
            => _queue.GetReportAsync(_queueId, ct);

        // ── Main loop ─────────────────────────────────────────────────────────────

        private async Task RunLoopAsync(CancellationToken ct)
        {
            var semaphore   = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);
            var activeTasks = new List<Task>();

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    _lastHeartbeatUtc = DateTimeOffset.UtcNow;
                    activeTasks.RemoveAll(t => t.IsCompleted);

                    var item = await _queue.GetNextItemAsync(
                        _queueId, botId: _botContext.BotId, ct: ct).ConfigureAwait(false);

                    if (item == null)
                    {
                        await Task.Delay(_pollInterval, ct).ConfigureAwait(false);
                        continue;
                    }

                    await semaphore.WaitAsync(ct).ConfigureAwait(false);
                    Interlocked.Increment(ref _inFlightCount);

                    activeTasks.Add(Task.Run(async () =>
                    {
                        try   { await ProcessItemAsync(item, ct).ConfigureAwait(false); }
                        finally
                        {
                            semaphore.Release();
                            Interlocked.Decrement(ref _inFlightCount);
                        }
                    }, ct));
                }
            }
            catch (OperationCanceledException) { }

            await Task.WhenAll(activeTasks).ConfigureAwait(false);
        }

        private async Task ProcessItemAsync(IWorkItem item, CancellationToken ct)
        {
            try
            {
                var context = _contextFactory(item);
                var result  = await _runtime.ExecuteAsync(context, ct).ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    await _queue.MarkCompletedAsync(
                        item.ItemId,
                        result.Outputs.Count > 0 ? result.Outputs : null,
                        ct).ConfigureAwait(false);
                }
                else
                {
                    var ex = result.Exception;
                    await _queue.MarkFailedAsync(
                        item.ItemId,
                        result.ErrorMessage ?? "Execution failed",
                        exceptionType:   ex?.GetType().Name,
                        exceptionDetail: ex?.ToString(),
                        maxAttempts:     _maxAttempts,
                        ct:              ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                await _queue.MarkFailedAsync(
                    item.ItemId,
                    ex.Message,
                    exceptionType:   ex.GetType().Name,
                    exceptionDetail: ex.ToString(),
                    maxAttempts:     _maxAttempts,
                    ct:              ct).ConfigureAwait(false);
            }
        }

        // ── Disposal ─────────────────────────────────────────────────────────────

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            _cts?.Dispose();
            await _runtime.DisposeAsync().ConfigureAwait(false);
        }
    }
}

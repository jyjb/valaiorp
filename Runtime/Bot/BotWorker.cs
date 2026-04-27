namespace Valaiorp.Runtime.Bot
{
    using Valaiorp.Core.Contracts;

    /// <summary>
    /// Continuously running bot that pulls IWorkItems from a shared IWorkQueue and
    /// executes each one through AgentRuntime.
    ///
    /// Deploy N BotWorkers across N machines all pointing at the same IWorkQueue
    /// backend (SqlWorkQueue for multi-machine, InMemoryWorkQueue for single-process).
    ///
    /// Lifecycle:
    ///   StartAsync()  — registers a run, begins the poll loop (non-blocking)
    ///   StopAsync()   — drains in-flight work, ends the run, then stops
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

        private CancellationTokenSource? _cts;
        private Task?                    _loopTask;
        private string?                  _runId;

        public BotWorker(
            AgentRuntime                       runtime,
            IWorkQueue                         queue,
            IBotContext                        botContext,
            string                             queueId,
            Func<IWorkItem, IExecutionContext> contextFactory,
            int      maxConcurrency = 4,
            int      maxAttempts    = 3,
            TimeSpan pollInterval   = default)
        {
            _runtime        = runtime;
            _queue          = queue;
            _botContext     = botContext;
            _queueId        = queueId;
            _contextFactory = contextFactory;
            _maxConcurrency = maxConcurrency;
            _maxAttempts    = maxAttempts;
            _pollInterval   = pollInterval == default ? TimeSpan.FromMilliseconds(500) : pollInterval;
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
        }

        /// <summary>Signals stop, drains in-flight items, and ends the queue run.</summary>
        public async Task StopAsync()
        {
            if (_cts != null) await _cts.CancelAsync().ConfigureAwait(false);
            if (_loopTask != null) await _loopTask.ConfigureAwait(false);
            if (_runId != null) await _queue.EndRunAsync(_runId).ConfigureAwait(false);
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
                    activeTasks.RemoveAll(t => t.IsCompleted);

                    var item = await _queue.GetNextItemAsync(
                        _queueId, botId: _botContext.BotId, ct: ct).ConfigureAwait(false);

                    if (item == null)
                    {
                        await Task.Delay(_pollInterval, ct).ConfigureAwait(false);
                        continue;
                    }

                    await semaphore.WaitAsync(ct).ConfigureAwait(false);

                    activeTasks.Add(Task.Run(async () =>
                    {
                        try   { await ProcessItemAsync(item, ct).ConfigureAwait(false); }
                        finally { semaphore.Release(); }
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

namespace Valaiorp.Runtime
{
    using Microsoft.Extensions.DependencyInjection;
    using Valaiorp.Configuration.Config;
    using Valaiorp.Core.Contracts;
    using Valaiorp.Core.Enums;
    using Valaiorp.Execution.Executors;
    using Valaiorp.Execution.Models;
    using Valaiorp.Execution.Transactions;
    using Valaiorp.Knowledge.Resolvers;
    using Valaiorp.Memory.Contracts;
    using Valaiorp.Observability.Contracts;
    using Valaiorp.Observability.Tracing;
    using Valaiorp.Planner.Orchestration;
    using Valaiorp.Policy.Contracts;
    using Valaiorp.Guardrails.Contracts;
    using Valaiorp.Guardrails.Enums;
    using Valaiorp.Tools.Enhanced.Logging;
    using Valaiorp.Tools.Resolvers;

    public sealed class AgentRuntime : IDisposable, IAsyncDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ExecutionTracer _tracer;
        private readonly ParallelExecutor _executor;
        private readonly PlannerOrchestrator _plannerOrchestrator;
        private readonly ToolResolver _toolResolver;
        private readonly TransactionManager _transactionManager;
        private readonly IPolicyEngine _policyEngine;
        private readonly IGuardrailPipeline _guardrails;
        private readonly IExecutionLogger _executionLogger;
        private readonly ValaiorpConfig _config;
        private readonly IShortTermMemory _shortTermMemory;
        private readonly ILongTermMemory _longTermMemory;
        private readonly KnowledgeProviderResolver _knowledgeResolver;
        private readonly ILogger _logger;

        private ExecutionMode _currentMode = ExecutionMode.Sequential;
        private bool _disposed;

        public AgentRuntime(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _tracer = serviceProvider.GetRequiredService<ExecutionTracer>();
            _executor = serviceProvider.GetRequiredService<ParallelExecutor>();
            _plannerOrchestrator = serviceProvider.GetRequiredService<PlannerOrchestrator>();
            _toolResolver = serviceProvider.GetRequiredService<ToolResolver>();
            _transactionManager = serviceProvider.GetRequiredService<TransactionManager>();
            _policyEngine = serviceProvider.GetRequiredService<IPolicyEngine>();
            _guardrails   = serviceProvider.GetRequiredService<IGuardrailPipeline>();
            _executionLogger = serviceProvider.GetRequiredService<IExecutionLogger>();
            _config = serviceProvider.GetRequiredService<ValaiorpConfig>();
            _shortTermMemory = serviceProvider.GetRequiredService<IShortTermMemory>();
            _longTermMemory = serviceProvider.GetRequiredService<ILongTermMemory>();
            _knowledgeResolver = serviceProvider.GetRequiredService<KnowledgeProviderResolver>();
            _logger = serviceProvider.GetRequiredService<ILogger>();
        }

        public ExecutionMode CurrentMode
        {
            get => _currentMode;
            set => _currentMode = value;
        }

        // ── Single-agent execution ────────────────────────────────────────────────

        public async Task<IExecutionResult> ExecuteAsync(
            IExecutionContext context,
            CancellationToken ct = default)
        {
            var enriched = await EnrichContextWithRagAndMemoryAsync(context, ct)
                .ConfigureAwait(false);

            var result = await _tracer.TraceAsync(
                "AgentRuntime.Execute",
                enriched.Id,
                async (_, token) =>
                {
                    // Guardrail — input check
                    var inputContent = enriched.Prompt?.UserPrompt ?? string.Empty;
                    if (!string.IsNullOrEmpty(inputContent))
                    {
                        var gr = await _guardrails
                            .EvaluateInputAsync(enriched, inputContent, token).ConfigureAwait(false);
                        if (!gr.IsAllowed)
                            return new ExecutionResult(enriched.Id, enriched.Id, false,
                                $"[Guardrail:{gr.GuardrailId}] {gr.Reason}");

                        if (gr.Action == ViolationAction.Redact && gr.SafeContent is not null
                            && enriched.Prompt is not null)
                            enriched = ApplyInputRedaction(enriched, gr.SafeContent);
                    }

                    var prePolicyResult = await _policyEngine
                        .EvaluatePreExecutionAsync(enriched, token).ConfigureAwait(false);
                    if (!prePolicyResult.IsAllowed)
                        return new ExecutionResult(enriched.Id, enriched.Id, false, prePolicyResult.Reason);

                    var plan = await _plannerOrchestrator
                        .CreatePlanAsync(null, enriched, token).ConfigureAwait(false);
                    await _executionLogger.LogPlanAsync(plan, enriched, token).ConfigureAwait(false);

                    var unit = new ExecutionUnit
                    {
                        Context   = enriched,
                        Plan      = plan,
                        ContextId = enriched.Id
                    };
                    foreach (var step in plan.Steps)
                        unit.Graph.AddNode(step);

                    // Wire sequential ordering: each non-parallel step depends on its predecessor,
                    // guaranteeing variable bindings resolve after the producing step finishes.
                    for (int i = 1; i < plan.Steps.Count; i++)
                    {
                        if (plan.Steps[i].Mode != Core.Enums.ExecutionMode.Parallel)
                            unit.Graph.AddDependency(plan.Steps[i].Id, plan.Steps[i - 1].Id);
                    }

                    _transactionManager.BeginTransaction(unit);
                    try
                    {
                        await _executor.ExecuteAsync(unit, token).ConfigureAwait(false);

                        foreach (var node in unit.Graph.Nodes.Values)
                            await _executionLogger.LogStepAsync(node, enriched, token).ConfigureAwait(false);

                        var stepOutputs = unit.Graph.Nodes.Values
                            .Where(n => n.Result != null)
                            .ToDictionary(
                                n => n.Step.Name,
                                n => (object)n.Result!.Outputs);

                        var execResult = new ExecutionResult(
                            enriched.Id, enriched.Id,
                            unit.Status == ExecutionStatus.Completed,
                            unit.Exception?.Message,
                            unit.Exception,
                            stepOutputs,
                            unit.CompletedAt.GetValueOrDefault(DateTimeOffset.UtcNow) - unit.StartedAt);

                        await _executionLogger.LogRunAsync(unit, enriched, token).ConfigureAwait(false);

                        // Guardrail — output check against actual tool output content
                        var outputContent = execResult.IsSuccess
                            ? System.Text.Json.JsonSerializer.Serialize(
                                unit.Graph.Nodes.Values
                                    .Where(n => n.Result != null)
                                    .ToDictionary(n => n.Step.Name, n => n.Result!.Outputs))
                            : execResult.ErrorMessage ?? string.Empty;
                        if (!string.IsNullOrEmpty(outputContent))
                        {
                            var gr = await _guardrails
                                .EvaluateOutputAsync(enriched, outputContent, token).ConfigureAwait(false);
                            if (!gr.IsAllowed)
                            {
                                await _transactionManager.RollbackAsync(unit, token).ConfigureAwait(false);
                                return new ExecutionResult(enriched.Id, enriched.Id, false,
                                    $"[Guardrail:{gr.GuardrailId}] {gr.Reason}");
                            }
                        }

                        var postPolicyResult = await _policyEngine
                            .EvaluatePostExecutionAsync(execResult, token).ConfigureAwait(false);
                        if (!postPolicyResult.IsAllowed)
                        {
                            await _transactionManager.RollbackAsync(unit, token).ConfigureAwait(false);
                            return new ExecutionResult(enriched.Id, enriched.Id, false, postPolicyResult.Reason);
                        }

                        _transactionManager.Commit(unit);

                        // Persist execution log to long-term memory (best-effort — failure does not abort)
                        await PersistExecutionToMemoryAsync(enriched, execResult, token)
                            .ConfigureAwait(false);

                        return execResult;
                    }
                    catch (Exception ex)
                    {
                        unit.Exception = ex;
                        unit.Status = ExecutionStatus.Failed;
                        unit.CompletedAt = DateTimeOffset.UtcNow;
                        await _transactionManager.RollbackAsync(unit, token).ConfigureAwait(false);
                        await _executionLogger.LogRunAsync(unit, enriched, token).ConfigureAwait(false);
                        throw;
                    }
                },
                ct).ConfigureAwait(false);

            return result;
        }

        // ── Guardrail helpers ─────────────────────────────────────────────────────

        private static IExecutionContext ApplyInputRedaction(IExecutionContext context, string redactedPrompt)
            => new Core.Contracts.ExecutionContext
            {
                Id                = context.Id,
                SessionId         = context.SessionId,
                UserId            = context.UserId,
                ExpiresAt         = context.ExpiresAt,
                Metadata          = context.Metadata,
                Steps             = context.Steps,
                CancellationToken = context.CancellationToken,
                Prompt = new PromptContext
                {
                    SystemPrompt        = context.Prompt!.SystemPrompt,
                    UserPrompt          = redactedPrompt,
                    RagContext          = context.Prompt.RagContext,
                    MemoryContext       = context.Prompt.MemoryContext,
                    ConversationHistory = context.Prompt.ConversationHistory,
                    Variables           = context.Prompt.Variables
                }
            };

        // ── RAG + Memory enrichment ───────────────────────────────────────────────

        /// <summary>
        /// Hydrates the context's PromptContext with RAG results and memory entries
        /// before handing it to the planner.
        /// </summary>
        private async Task<IExecutionContext> EnrichContextWithRagAndMemoryAsync(
            IExecutionContext context,
            CancellationToken ct)
        {
            if (context.Prompt == null)
                return context; // no prompt — nothing to enrich

            var ragContext = context.Prompt.RagContext;
            var memoryContext = context.Prompt.MemoryContext;

            // RAG: query the default knowledge provider only if one is actually registered
            if (ragContext.Count == 0 && !string.IsNullOrWhiteSpace(context.Prompt.UserPrompt)
                && _knowledgeResolver.GetDefaultProvider() != null)
            {
                try
                {
                    var results = await _knowledgeResolver.SearchAsync(
                        providerId: null,
                        query: context.Prompt.UserPrompt,
                        maxResults: _config.Knowledge.MaxKnowledgeResults,
                        ct: ct).ConfigureAwait(false);
                    ragContext = [.. results];
                }
                catch (Exception ex)
                {
                    await _logger.LogAsync(LogLevel.Warning,
                        $"[AgentRuntime] RAG enrichment failed for session '{context.SessionId}': {ex.Message}",
                        correlationId: context.SessionId, ct: ct).ConfigureAwait(false);
                }
            }

            // Memory: retrieve recent entries stored for this session (best-effort)
            if (memoryContext.Count == 0)
            {
                try
                {
                    var sessionKey = $"memory:{context.SessionId}";
                    var stored = await _shortTermMemory
                        .GetAsync<List<string>>(sessionKey, ct).ConfigureAwait(false);
                    if (stored?.Count > 0)
                        memoryContext = stored;
                }
                catch (Exception ex)
                {
                    await _logger.LogAsync(LogLevel.Warning,
                        $"[AgentRuntime] Memory retrieval failed for session '{context.SessionId}': {ex.Message}",
                        correlationId: context.SessionId, ct: ct).ConfigureAwait(false);
                }
            }

            if (ragContext == context.Prompt.RagContext && memoryContext == context.Prompt.MemoryContext)
                return context; // nothing changed

            // Return a new context with the enriched prompt (non-destructive)
            return new Core.Contracts.ExecutionContext
            {
                Id             = context.Id,
                SessionId      = context.SessionId,
                UserId         = context.UserId,
                ExpiresAt      = context.ExpiresAt,
                Metadata       = context.Metadata,
                Steps          = context.Steps,
                CancellationToken = context.CancellationToken,
                Prompt = new PromptContext
                {
                    SystemPrompt        = context.Prompt.SystemPrompt,
                    UserPrompt          = context.Prompt.UserPrompt,
                    RagContext          = ragContext,
                    MemoryContext       = memoryContext,
                    ConversationHistory = context.Prompt.ConversationHistory,
                    Variables           = context.Prompt.Variables
                }
            };
        }

        private async Task PersistExecutionToMemoryAsync(
            IExecutionContext context,
            IExecutionResult result,
            CancellationToken ct)
        {
            try
            {
                var sessionKey = $"memory:{context.SessionId}";
                var existing = await _shortTermMemory
                    .GetAsync<List<string>>(sessionKey, ct).ConfigureAwait(false) ?? [];

                existing.Add($"[{DateTimeOffset.UtcNow:u}] {context.Prompt?.UserPrompt} → {(result.IsSuccess ? "success" : result.ErrorMessage)}");

                await _shortTermMemory.SetAsync(sessionKey, existing, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _logger.LogAsync(LogLevel.Warning,
                    $"[AgentRuntime] Memory persistence failed for session '{context.SessionId}': {ex.Message}",
                    correlationId: context.SessionId, ct: ct).ConfigureAwait(false);
            }
        }

        // ── Mode switching ────────────────────────────────────────────────────────

        public void SwitchMode(ExecutionMode mode)
        {
            if (_config.Execution.Mode == ExecutionMode.Sequential && mode == ExecutionMode.Parallel)
                throw new InvalidOperationException(
                    "Cannot switch to Parallel mode: configuration was set to Sequential. " +
                    "Set Execution.Mode = \"Parallel\" (or \"Hybrid\") in valaiorp.json to enable runtime mode switching.");
            _currentMode = mode;
        }

        public T GetService<T>() where T : notnull
            => _serviceProvider.GetRequiredService<T>();

        // ── Disposal ─────────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_serviceProvider is IDisposable d) d.Dispose();
                _disposed = true;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                if (_serviceProvider is IAsyncDisposable ad) await ad.DisposeAsync().ConfigureAwait(false);
                else if (_serviceProvider is IDisposable d) d.Dispose();
                _disposed = true;
            }
        }
    }
}

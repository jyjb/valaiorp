namespace Valaiorp.MultiAgent.Orchestration
{
    using Valaiorp.Core.Contracts;
    using Valaiorp.Memory.Contracts;
    using Valaiorp.MultiAgent.Contracts;

    /// <summary>
    /// Drives a multi-agent conversation.
    ///
    /// Flow:
    ///   1. Route the initial AgentMessage to the orchestrator agent.
    ///   2. Orchestrator produces an AgentResult; if it contains DelegatedMessages,
    ///      dispatch each to the named sub-agent.
    ///   3. Sub-agent results are stored in IConversationMemory and fed back to the
    ///      orchestrator as a synthesis turn.
    ///   4. Repeat until the orchestrator returns no further delegations (task complete).
    /// </summary>
    public sealed class MultiAgentOrchestrator
    {
        private readonly IAgentRegistry _registry;
        private readonly IConversationMemory _memory;

        /// <summary>Maximum delegation rounds before forcibly stopping (prevents infinite loops).</summary>
        public int MaxRounds { get; set; } = 20;

        public MultiAgentOrchestrator(IAgentRegistry registry, IConversationMemory memory)
        {
            _registry = registry;
            _memory = memory;
        }

        public async Task<AgentResult> RunAsync(AgentMessage initialMessage, CancellationToken ct = default)
        {
            var orchestrator = _registry.GetOrchestrator()
                ?? throw new InvalidOperationException(
                    "No orchestrator agent registered. " +
                    "Call IAgentRegistry.Register(agent, setAsDefaultOrchestrator: true) first.");

            // Store opening user message
            await _memory.AddTurnAsync(
                initialMessage.ConversationId,
                new ConversationTurn { Role = "user", Content = initialMessage.Prompt.UserPrompt },
                ct).ConfigureAwait(false);

            var currentMessage = initialMessage;
            AgentResult? lastResult = null;
            var round = 0;

            while (round++ < MaxRounds && !ct.IsCancellationRequested)
            {
                // Inject conversation history into the current message's prompt
                currentMessage = await InjectHistoryAsync(currentMessage, ct).ConfigureAwait(false);

                // Run orchestrator
                lastResult = await orchestrator.RunAsync(currentMessage, ct).ConfigureAwait(false);

                await _memory.AddTurnAsync(
                    currentMessage.ConversationId,
                    new ConversationTurn
                    {
                        Role    = orchestrator.AgentId,
                        Content = lastResult.Output ?? string.Empty
                    }, ct).ConfigureAwait(false);

                if (!lastResult.IsSuccess || lastResult.DelegatedMessages.Count == 0)
                    break;

                // Dispatch delegated sub-tasks and collect results
                var subResults = await DispatchDelegationsAsync(lastResult.DelegatedMessages, ct)
                    .ConfigureAwait(false);

                // Build synthesis message so the orchestrator can integrate sub-results
                currentMessage = BuildSynthesisMessage(initialMessage, subResults);
            }

            return lastResult ?? AgentResult.Failure(
                orchestrator.AgentId,
                initialMessage.ConversationId,
                "Orchestrator produced no result.");
        }

        // ── Sub-agent dispatch ────────────────────────────────────────────────────

        private async Task<IReadOnlyList<AgentResult>> DispatchDelegationsAsync(
            IReadOnlyList<AgentMessage> delegations,
            CancellationToken ct)
        {
            var results = new List<AgentResult>(delegations.Count);

            // Separate parallel-eligible vs sequential delegations
            var parallel = delegations.Where(m => IsParallel(m)).ToList();
            var sequential = delegations.Where(m => !IsParallel(m)).ToList();

            // Parallel batch
            if (parallel.Count > 0)
            {
                var tasks = parallel.Select(msg => RunSubAgentAsync(msg, ct));
                var parallelResults = await Task.WhenAll(tasks).ConfigureAwait(false);
                results.AddRange(parallelResults);
            }

            // Sequential batch
            foreach (var msg in sequential)
            {
                var r = await RunSubAgentAsync(msg, ct).ConfigureAwait(false);
                results.Add(r);
            }

            return results;
        }

        private async Task<AgentResult> RunSubAgentAsync(AgentMessage msg, CancellationToken ct)
        {
            if (msg.ToAgentId == null)
                return AgentResult.Failure(string.Empty, msg.ConversationId, "ToAgentId is null on delegation.");

            var agent = _registry.Resolve(msg.ToAgentId);
            if (agent == null)
                return AgentResult.Failure(msg.ToAgentId, msg.ConversationId,
                    $"Agent '{msg.ToAgentId}' not found in registry.");

            var enriched = await InjectHistoryAsync(msg, ct).ConfigureAwait(false);
            var result = await agent.RunAsync(enriched, ct).ConfigureAwait(false);

            await _memory.AddTurnAsync(
                msg.ConversationId,
                new ConversationTurn { Role = agent.AgentId, Content = result.Output ?? string.Empty },
                ct).ConfigureAwait(false);

            return result;
        }

        // ── Synthesis message ────────────────────────────────────────────────────

        private static AgentMessage BuildSynthesisMessage(
            AgentMessage original,
            IReadOnlyList<AgentResult> subResults)
        {
            var summary = string.Join("\n", subResults.Select((r, i) =>
                $"Sub-agent [{r.AgentId}] result {i + 1}: {(r.IsSuccess ? r.Output : $"ERROR: {r.Error}")}"));

            return new AgentMessage
            {
                ConversationId = original.ConversationId,
                FromAgentId    = null,
                ToAgentId      = null,
                Prompt = new PromptContext
                {
                    SystemPrompt        = original.Prompt.SystemPrompt,
                    UserPrompt          = $"Sub-agent results:\n{summary}\n\nContinue or finalise the task.",
                    RagContext          = original.Prompt.RagContext,
                    MemoryContext       = original.Prompt.MemoryContext,
                    Variables           = original.Prompt.Variables
                }
            };
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private async Task<AgentMessage> InjectHistoryAsync(AgentMessage msg, CancellationToken ct)
        {
            var history = await _memory.GetRecentHistoryAsync(msg.ConversationId, 20, ct)
                .ConfigureAwait(false);

            return new AgentMessage
            {
                ConversationId = msg.ConversationId,
                FromAgentId    = msg.FromAgentId,
                ToAgentId      = msg.ToAgentId,
                Payload        = msg.Payload,
                Prompt = new PromptContext
                {
                    SystemPrompt        = msg.Prompt.SystemPrompt,
                    UserPrompt          = msg.Prompt.UserPrompt,
                    RagContext          = msg.Prompt.RagContext,
                    MemoryContext       = msg.Prompt.MemoryContext,
                    ConversationHistory = history,
                    Variables           = msg.Prompt.Variables
                }
            };
        }

        private static bool IsParallel(AgentMessage msg)
            => msg.Payload.TryGetValue("parallel", out var v) && v is true or "true";
    }
}

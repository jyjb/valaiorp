namespace Valaiorp.Execution.Models
{
    using Valaiorp.Configuration.Models;
    using Valaiorp.Core.Contracts;

    public sealed class AutonomyContext
    {
        public AutonomyConfig Config { get; }
        public IExecutionContext ExecutionContext { get; }

        public AutonomyContext(AutonomyConfig config, IExecutionContext executionContext)
        {
            Config = config;
            ExecutionContext = executionContext;
        }

        public bool CanModifyPlan => Config.AllowDynamicPlanning && Config.Level >= 0.3;
        public bool CanSelectTools => Config.AllowToolSelection && Config.Level >= 0.7;
        public bool CanBranchDynamically => Config.AllowConditionalBranching;
        public bool RequiresApproval => Config.RequireApprovalForHighRisk && Config.Level < 1.0;
    }
}
using System;
using Valaiorp.Core.Enums;
namespace Valaiorp.Configuration.Models
{
    public sealed class AutonomyConfig
    {
        public double Level { get; set; } = 0.0; // 0.0 (deterministic) to 1.0 (fully agentic)
        public AutonomyMode Mode
        {
            get
            {
                return Level switch
                {
                    <= 0.1 => AutonomyMode.FullyDeterministic,
                    <= 0.4 => AutonomyMode.ControlledHybrid,
                    <= 0.7 => AutonomyMode.AssistedAgentic,
                    _ => AutonomyMode.FullyAgentic
                };
            }
            set
            {
                Level = value switch
                {
                    AutonomyMode.FullyDeterministic => 0.0,
                    AutonomyMode.ControlledHybrid => 0.3,
                    AutonomyMode.AssistedAgentic => 0.7,
                    AutonomyMode.FullyAgentic => 1.0,
                    _ => 0.0
                };
            }
        }
        public bool AllowDynamicPlanning { get; set; } = false;
        public bool AllowToolSelection { get; set; } = false;
        public bool AllowConditionalBranching { get; set; } = true;
        public bool RequireApprovalForHighRisk { get; set; } = false;
    }
}
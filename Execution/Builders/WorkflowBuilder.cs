namespace Valaiorp.Execution.Builders
{
    using Valaiorp.Core.Enums;
    using Valaiorp.Execution.Models;

    public sealed class WorkflowBuilder
    {
        private readonly List<WorkflowStep> _steps = new();
        private string _lastStepId = string.Empty;

        public WorkflowBuilder AddStep(
            string name,
            string description,
            ToolType toolType,
            string toolId,
            string input,
            string? nextStepId = null)
        {
            var step = new WorkflowStep
            {
                Name        = name,
                Description = description,
                Tool        = toolType,
                ToolId      = toolId,
                Input       = input,
                NextStepId  = nextStepId
            };
            _steps.Add(step);
            _lastStepId = step.Id;
            return this;
        }

        public WorkflowBuilder WithCondition(string condition)
        {
            GetLastStep()?.Let(s => s.Condition = condition);
            return this;
        }

        public WorkflowBuilder AsLoopStart()
        {
            GetLastStep()?.Let(s => s.IsLoopStart = true);
            return this;
        }

        public WorkflowBuilder AsLoopEnd(string loopCondition)
        {
            GetLastStep()?.Let(s =>
            {
                s.IsLoopEnd = true;
                s.LoopCondition = loopCondition;
            });
            return this;
        }

        public WorkflowBuilder Then(string nextStepName)
        {
            var current = GetLastStep();
            var next = _steps.FirstOrDefault(s => s.Name == nextStepName);
            if (current != null && next != null)
            {
                current.NextStepId = next.Id;
            }
            return this;
        }

        public WorkflowBuilder ThenIf(string condition, string trueStepName, string falseStepName)
        {
            var current = GetLastStep();
            if (current == null) return this;

            var trueStep  = _steps.FirstOrDefault(s => s.Name == trueStepName);
            var falseStep = _steps.FirstOrDefault(s => s.Name == falseStepName);

            current.Condition  = condition;
            current.NextStepId = trueStep?.Id;

            if (falseStep != null)
            {
                // Convention: '!' prefix on NextStepId signals the false branch
                current.NextStepId = $"{trueStep?.Id}|!{falseStep.Id}";
            }

            return this;
        }

        public IReadOnlyList<WorkflowStep> Build() => _steps.AsReadOnly();

        private WorkflowStep? GetLastStep()
            => string.IsNullOrEmpty(_lastStepId) ? null : _steps.FirstOrDefault(s => s.Id == _lastStepId);
    }

    file static class WorkflowBuilderExtensions
    {
        internal static T? Let<T>(this T? value, Action<T> action) where T : class
        {
            if (value != null) action(value);
            return value;
        }
    }
}

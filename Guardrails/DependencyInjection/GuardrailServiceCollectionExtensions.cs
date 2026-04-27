namespace Valaiorp.Guardrails.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;
    using Valaiorp.Guardrails.BuiltIn;
    using Valaiorp.Guardrails.Contracts;
    using Valaiorp.Guardrails.Enums;
    using Valaiorp.Guardrails.Pipeline;

    public static class GuardrailServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a pre-configured <see cref="IGuardrailPipeline"/> with the built-in
        /// guardrails enabled according to the provided options.
        /// </summary>
        public static IServiceCollection AddGuardrails(
            this IServiceCollection services,
            Action<GuardrailOptions>? configure = null)
        {
            var options = new GuardrailOptions();
            configure?.Invoke(options);

            services.AddSingleton<IGuardrailPipeline>(_ =>
            {
                var pipeline = new GuardrailPipeline();

                if (options.EnablePiiRedaction)
                    pipeline.Add(new PiiGuardrail());

                if (options.EnablePromptInjectionDetection)
                    pipeline.Add(new PromptInjectionGuardrail());

                if (options.EnableBannedKeywords && options.BannedKeywords?.Length > 0)
                    pipeline.Add(new BannedKeywordsGuardrail(options.BannedKeywords));

                if (options.MaxInputLengthChars > 0)
                    pipeline.Add(new ContentLengthGuardrail(GuardrailScope.Input, options.MaxInputLengthChars));

                if (options.MaxOutputLengthChars > 0)
                    pipeline.Add(new ContentLengthGuardrail(GuardrailScope.Output, options.MaxOutputLengthChars));

                if (options.AllowedToolIds?.Length > 0 || options.DeniedToolIds?.Length > 0)
                    pipeline.Add(new ToolScopeGuardrail(options.AllowedToolIds, options.DeniedToolIds));

                if (options.EnableDataClassification)
                    pipeline.Add(new DataClassificationGuardrail());

                foreach (var custom in options.CustomGuardrails)
                    pipeline.Add(custom);

                return pipeline;
            });

            return services;
        }
    }

    public sealed class GuardrailOptions
    {
        public bool EnablePiiRedaction               { get; set; } = false;
        public bool EnablePromptInjectionDetection   { get; set; } = true;
        public bool EnableBannedKeywords             { get; set; } = false;
        public string[]? BannedKeywords              { get; set; }
        public int MaxInputLengthChars               { get; set; } = 32_000;
        public int MaxOutputLengthChars              { get; set; } = 32_000;
        public string[]? AllowedToolIds              { get; set; }
        public string[]? DeniedToolIds               { get; set; }
        public bool EnableDataClassification         { get; set; } = false;

        internal readonly List<IGuardrail> CustomGuardrails = [];

        /// <summary>Add a custom IGuardrail implementation to the pipeline.</summary>
        public void AddCustomGuardrail(IGuardrail guardrail) => CustomGuardrails.Add(guardrail);
    }
}

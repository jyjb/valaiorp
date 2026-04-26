namespace Valaiorp.Configuration.Models
{
    public sealed class ParallelismConfig
    {
        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
        public int MaxConcurrentExecutions { get; set; } = 10;
        public bool EnableDynamicScaling { get; set; } = true;
        public int MinThreadPoolSize { get; set; } = 4;
        public int MaxThreadPoolSize { get; set; } = 50;
    }
}
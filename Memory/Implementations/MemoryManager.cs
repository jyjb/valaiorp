namespace Valaiorp.Memory.Implementations
{
    using Valaiorp.Memory.Contracts;

    public sealed class MemoryManager
    {
        private readonly IShortTermMemory _shortTermMemory;
        private readonly ILongTermMemory _longTermMemory;
        private readonly IConversationMemory _conversationMemory;

        public MemoryManager(
            IShortTermMemory shortTermMemory,
            ILongTermMemory longTermMemory,
            IConversationMemory conversationMemory)
        {
            _shortTermMemory = shortTermMemory;
            _longTermMemory = longTermMemory;
            _conversationMemory = conversationMemory;
        }

        public IShortTermMemory ShortTerm => _shortTermMemory;
        public ILongTermMemory LongTerm => _longTermMemory;
        public IConversationMemory Conversation => _conversationMemory;
    }
}
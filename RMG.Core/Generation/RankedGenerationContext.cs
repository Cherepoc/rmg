namespace RMG.Core.Generation
{
    public class RankedGenerationContext : GenerationContext
    {
        public RankedGenerationContext(GenerationContext parentContext, object value, int rank)
            : base(parentContext, value)
        {
            Rank = rank;
        }

        public int Rank { get; }
    }
}

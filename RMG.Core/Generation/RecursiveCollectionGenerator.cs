namespace RMG.Core.Generation
{
    public sealed class RecursiveCollectionGenerator<T>
    {
        public int MaxRank { get; set; }
        
        public IGenerator ElementGenerator { get; set; }
    }
}

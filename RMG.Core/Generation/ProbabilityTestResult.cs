namespace RMG.Core.Generation
{
    public readonly struct ProbabilityTestResult
    {
        public ProbabilityTestResult(int itemIndex, double testProbabilityRemainder)
        {
            ItemIndex = itemIndex;
            TestProbabilityRemainder = testProbabilityRemainder;
        }

        public int ItemIndex { get; }

        public double TestProbabilityRemainder { get; }
    }
}

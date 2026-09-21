using System.Collections.Immutable;
using Rmg.Core.Events;

namespace Rmg.Tests.StateMapBuilders;

public sealed class StateMapBuilderTest
{
    private static readonly StateKind<int> KeyOffset = StateKinds.KeyOffset;
    private static readonly StateKind<int> OctaveOffset = StateKinds.OctaveOffset;
    private static readonly StateKind<double> Tempo = StateKinds.Tempo;
    private static readonly StateKind<ImmutableArray<int>> ScaleOffsets = StateKinds.ScaleOffsets;

    private static readonly FakeGenerationContext Context = new() { NextDouble = 0.5 };

    [Test]
    public async Task Empty_ResultsIn_Default()
    {
        var result = new StateMapBuilder().ToStateMap(Context);

        await Assert.That(result.IsDefault).IsTrue();
    }

    [Test]
    public async Task AddState_IsIncluded()
    {
        var result = new StateMapBuilder()
            .Add(KeyOffset.CreateState(3))
            .ToStateMap(Context);

        await Assert.That(result.GetStateValue(KeyOffset)).IsEqualTo(3);
    }

    [Test]
    public async Task AddKindAndValue_IsIncluded()
    {
        var result = new StateMapBuilder()
            .Add(KeyOffset, 3)
            .ToStateMap(Context);

        await Assert.That(result.GetStateValue(KeyOffset)).IsEqualTo(3);
    }

    [Test]
    public async Task AddKindAndGenerator_UsesGeneratedValue()
    {
        var result = new StateMapBuilder()
            .Add(Tempo, context => context.GenerateDouble() * 4)
            .ToStateMap(Context);

        await Assert.That(result.GetStateValue(Tempo)).IsEqualTo(2);
    }

    [Test]
    public async Task AddKindAndGenerator_GeneratesAgainOnEachToStateMap()
    {
        var context = new FakeGenerationContext { NextDouble = 2 };
        var builder = new StateMapBuilder().Add(Tempo, c => c.GenerateDouble());

        var first = builder.ToStateMap(context);
        context.NextDouble = 3;
        var second = builder.ToStateMap(context);

        await Assert.That(first.GetStateValue(Tempo)).IsEqualTo(2);
        await Assert.That(second.GetStateValue(Tempo)).IsEqualTo(3);
    }

    [Test]
    public async Task AddCollectionOfOne_WrapsGeneratedValueInCollection()
    {
        var result = new StateMapBuilder()
            .AddCollectionOfOne(ScaleOffsets, _ => 4)
            .ToStateMap(Context);

        await Assert.That(result.GetStateValue(ScaleOffsets).AsEnumerable()).IsEquivalentTo(new[] { 4 });
    }

    [Test]
    public async Task AddCollectionOfOne_MultipleTimes_MergesValues()
    {
        var result = new StateMapBuilder()
            .AddCollectionOfOne(ScaleOffsets, _ => 4)
            .AddCollectionOfOne(ScaleOffsets, _ => 2)
            .ToStateMap(Context);

        await Assert.That(result.GetStateValue(ScaleOffsets).AsEnumerable()).IsEquivalentTo(new[] { 2, 4 });
    }

    [Test]
    public async Task AddStateMap_IsIncluded()
    {
        var stateMap = StateMap.FromStates([KeyOffset.CreateState(1), Tempo.CreateState(2)]);

        var result = new StateMapBuilder()
            .Add(stateMap)
            .ToStateMap(Context);

        await Assert.That(result).IsEqualTo(stateMap);
    }

    [Test]
    public async Task AddStateMapGenerator_ReceivesContext()
    {
        var result = new StateMapBuilder()
            .Add(context => StateMap.FromStates([Tempo.CreateState(context.GenerateDouble())]))
            .ToStateMap(Context);

        await Assert.That(result.GetStateValue(Tempo)).IsEqualTo(0.5);
    }

    [Test]
    public async Task MixedSources_AreAggregatedPerKind()
    {
        var result = new StateMapBuilder()
            .Add(KeyOffset, 1)
            .Add(KeyOffset.CreateState(2))
            .Add(StateMap.FromStates([KeyOffset.CreateState(4), OctaveOffset.CreateState(1)]))
            .ToStateMap(Context);

        await Assert.That(result.GetStateValue(KeyOffset)).IsEqualTo(7);
        await Assert.That(result.GetStateValue(OctaveOffset)).IsEqualTo(1);
    }

    [Test]
    public async Task DefaultValues_AreDropped()
    {
        var result = new StateMapBuilder()
            .Add(KeyOffset, 0)
            .ToStateMap(Context);

        await Assert.That(result.IsDefault).IsTrue();
    }

    [Test]
    public async Task AddMethods_ReturnSameBuilder()
    {
        var builder = new StateMapBuilder();

        await Assert.That(builder.Add(KeyOffset, 1)).IsSameReferenceAs(builder);
        await Assert.That(builder.Add(KeyOffset.CreateState(1))).IsSameReferenceAs(builder);
        await Assert.That(builder.Add(StateMap.Default)).IsSameReferenceAs(builder);
    }

    [Test]
    public async Task ToStateMapGenerator_ProducesSameResultAsToStateMap()
    {
        var builder = new StateMapBuilder()
            .Add(KeyOffset, 2)
            .Add(Tempo, c => c.GenerateDouble());

        var generator = builder.ToStateMapGenerator();

        await Assert.That(generator(Context)).IsEqualTo(builder.ToStateMap(Context));
    }

    [Test]
    public async Task ToStateMapGenerator_IsNotAffectedByLaterAdds()
    {
        var builder = new StateMapBuilder().Add(KeyOffset, 2);
        var generator = builder.ToStateMapGenerator();

        builder.Add(OctaveOffset, 5).Add(StateMap.FromStates([Tempo.CreateState(3)]));

        var result = generator(Context);
        await Assert.That(result.GetStateValue(KeyOffset)).IsEqualTo(2);
        await Assert.That(result.GetStateValue(OctaveOffset)).IsEqualTo(0);
        await Assert.That(result.GetStateValue(Tempo)).IsEqualTo(1);
    }
}

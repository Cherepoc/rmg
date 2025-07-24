using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Events;

public sealed class StateMapBuilder
{
    private readonly List<Func<IGenerationContext, IState>> _stateGenerators = [];
    private readonly List<Func<IGenerationContext, StateMap>> _stateMapGenerators = [];

    public StateMapBuilder Add<T>(State<T> state)
        where T : notnull
    {
        _stateGenerators.Add(_ => state);
        return this;
    }

    public StateMapBuilder Add<T>(StateKind<T> stateKind, T value)
        where T : notnull
    {
        _stateGenerators.Add(_ => stateKind.CreateState(value));
        return this;
    }

    public StateMapBuilder Add<T>(StateKind<T> stateKind, Func<IGenerationContext, T> generator)
        where T : notnull
    {
        _stateGenerators.Add(context => stateKind.CreateState(generator(context)));
        return this;
    }

    public StateMapBuilder AddCollectionOfOne<T>(StateKind<ImmutableArray<T>> stateKind, Func<IGenerationContext, T> generator)
        where T : notnull
    {
        _stateGenerators.Add(context => stateKind.CreateState([generator(context)]));
        return this;
    }

    public StateMapBuilder Add(Func<IGenerationContext, StateMap> stateMapGenerator)
    {
        _stateMapGenerators.Add(stateMapGenerator);
        return this;
    }

    public StateMapBuilder Add(StateMap stateMap)
    {
        _stateMapGenerators.Add(_ => stateMap);
        return this;
    }

    public StateMap ToStateMap(IGenerationContext context)
    {
        return GenerateStateMap(context, _stateGenerators, _stateMapGenerators);
    }

    public Func<IGenerationContext, StateMap> ToStateMapGenerator()
    {
        IEnumerable<Func<IGenerationContext, IState>> fixedStateGenerators = [.._stateGenerators];
        IEnumerable<Func<IGenerationContext, StateMap>> fixedStateMapGenerators = [.._stateMapGenerators];
        return context => GenerateStateMap(context, fixedStateGenerators, fixedStateMapGenerators);
    }

    private static StateMap GenerateStateMap(
        IGenerationContext context,
        IEnumerable<Func<IGenerationContext, IState>> stateGenerators,
        IEnumerable<Func<IGenerationContext, StateMap>> stateMapGenerators
    )
    {
        var stateMapStates = stateMapGenerators.SelectMany(x => x(context).States);
        return stateGenerators
            .Select(generator => generator(context))
            .Concat(stateMapStates)
            .ToStateMap();
    }
}

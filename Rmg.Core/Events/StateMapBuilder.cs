using System.Collections.Immutable;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Events;

public sealed class StateMapBuilder
{
    private readonly string? _layer;
    private readonly bool _isPerTrack;
    private readonly List<Func<IGenerationContext, IState>> _stateGenerators = [];
    private readonly List<Func<IGenerationContext, StateMap>> _stateMapGenerators = [];

    public StateMapBuilder()
    {
    }

    /// <param name="layer">
    ///     The layer the states made here belong to, such as the song or a section, which a <see cref="StateTrace" />
    ///     records as their origin.
    /// </param>
    /// <param name="perTrack">
    ///     Whether only some tracks see the layer, such as a single track's or the drums', so that it may not set what
    ///     every track must see the same, such as the scale; every map it makes is checked for that.
    /// </param>
    public StateMapBuilder(string layer, bool perTrack = false)
    {
        _layer = layer;
        _isPerTrack = perTrack;
    }

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
        return GenerateStateMap(context, _layer, _isPerTrack, _stateGenerators, _stateMapGenerators);
    }

    public Func<IGenerationContext, StateMap> ToStateMapGenerator()
    {
        var layer = _layer;
        var isPerTrack = _isPerTrack;
        IEnumerable<Func<IGenerationContext, IState>> fixedStateGenerators = [.._stateGenerators];
        IEnumerable<Func<IGenerationContext, StateMap>> fixedStateMapGenerators = [.._stateMapGenerators];
        return context => GenerateStateMap(context, layer, isPerTrack, fixedStateGenerators, fixedStateMapGenerators);
    }

    private static StateMap GenerateStateMap(
        IGenerationContext context,
        string? layer,
        bool isPerTrack,
        IEnumerable<Func<IGenerationContext, IState>> stateGenerators,
        IEnumerable<Func<IGenerationContext, StateMap>> stateMapGenerators
    )
    {
        // the states made here are this layer's; those of the maps added keep the layers they were made in
        var states = stateGenerators.Select(generator => generator(context));
        if (layer is not null && StateTrace.IsRunning)
            states = states.Select(x => x.Contributions.IsEmpty ? x.WithLayer(layer) : x);

        var stateMapStates = stateMapGenerators.SelectMany(x => x(context).States);
        var stateMap = states
            .Concat(stateMapStates)
            .ToStateMap();
        return isPerTrack ? stateMap.ThrowIfShared(layer!) : stateMap;
    }
}

using System.Reflection;
using Rmg.Core.Events;

namespace Rmg.Tests.StateKindRegistry;

public sealed class StateKindsRegistryTest
{
    private static IEnumerable<IStateKind> DeclaredKinds()
    {
        return typeof(StateKinds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(x => typeof(IStateKind).IsAssignableFrom(x.FieldType))
            .Select(x => (IStateKind)x.GetValue(null)!);
    }

    [Test]
    public async Task DeclaredKinds_AreRenderKinds()
    {
        var notRender = DeclaredKinds()
            .Where(x => x.Scope != StateScope.Render)
            .Select(x => x.Name)
            .ToArray();

        await Assert.That(notRender).IsEquivalentTo(Array.Empty<string>());
    }

    [Test]
    public async Task KeyScaleAndTempo_AreTheOnlySharedDeclaredKinds_WithTheScalesRaisedSteps()
    {
        var shared = DeclaredKinds()
            .Where(x => x.IsShared)
            .ToArray();

        await Assert.That(shared).IsEquivalentTo(new IStateKind[] { StateKinds.KeyOffset, StateKinds.ScaleOffsets, StateKinds.RaisedScaleSteps, StateKinds.Tempo });
    }

    [Test]
    public async Task CreatedKind_IsACompositionKind_NotShared_UnlessAsked()
    {
        var kind = StateKinds.CreateAdditive<int>("StateKindsRegistryTest.Plain");
        var shared = StateKinds.CreateAdditive<int>("StateKindsRegistryTest.Shared", StateScope.Render, isShared: true);

        await Assert.That(kind.Scope).IsEqualTo(StateScope.Composition);
        await Assert.That(kind.IsShared).IsFalse();
        await Assert.That(shared.Scope).IsEqualTo(StateScope.Render);
        await Assert.That(shared.IsShared).IsTrue();
    }

    [Test]
    public async Task KindWithATakenName_ResultsIn_ArgumentException()
    {
        // a kind's name is its identity, so a second kind cannot take it, whatever its type
        await Assert.That(() => StateKinds.CreateAdditive<int>(StateKinds.Velocity.Name)).Throws<ArgumentException>();
        await Assert.That(() => StateKinds.CreateCollection<double>(StateKinds.Velocity.Name)).Throws<ArgumentException>();
    }

    [Test]
    public async Task KindWithANewName_IsCreated()
    {
        var kind = StateKinds.CreateAdditive<int>("StateKindsRegistryTest.NewKind");

        await Assert.That(kind.Name).IsEqualTo("StateKindsRegistryTest.NewKind");
    }
}

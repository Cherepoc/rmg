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
    public async Task GetAll_ContainsEveryDeclaredKind()
    {
        var registered = StateKinds.GetAll();

        var missing = DeclaredKinds()
            .Where(x => !registered.Contains(x))
            .Select(x => x.Name)
            .ToArray();

        await Assert.That(missing).IsEquivalentTo(Array.Empty<string>());
    }

    [Test]
    public async Task DeclaredKindNames_AreUnique()
    {
        var names = DeclaredKinds().Select(x => x.Name).ToArray();

        await Assert.That(names.Distinct().Count()).IsEqualTo(names.Length);
    }
}

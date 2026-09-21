using TUnit.Assertions;
using TUnit.Assertions.Sources;

namespace Rmg.Tests;

/// <summary>
/// Value-style <c>Assert.That</c>. Plain <c>Assert.That</c> on an <see cref="IEnumerable{T}"/> implementer
/// binds to the collection overloads and types <c>Satisfies</c> lambdas as the interface, hiding members like <c>IsEmpty</c>.
/// </summary>
public static class Check
{
#pragma warning disable TUnitAssertions0002
    public static ValueAssertion<T> That<T>(T value) => Assert.That<T>(value);
#pragma warning restore TUnitAssertions0002
}

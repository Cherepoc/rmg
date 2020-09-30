namespace RMG.TestsF

open FluentAssertions.Equivalency
open FluentAssertions.Primitives
open FluentAssertions

module Assertions =
    let optionConfiguration<'T> (options: EquivalencyAssertionOptions<'T>) =
        options.WithStrictOrdering()

    let should assertion expected actual = assertion (actual.Should(), expected)

    let beEquivalentTo (assertions: ObjectAssertions, expected) =
        assertions.BeEquivalentTo(expected, optionConfiguration, null)

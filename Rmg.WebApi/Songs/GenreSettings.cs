using System.Collections.Immutable;
using System.Text;
using Rmg.Core;
using Rmg.Core.Composition;

namespace Rmg.WebApi.Songs;

/// <summary>
///     A song's genre as the page had it (<see cref="Genre" />): its name, which only shows it, and every option it weighs,
///     by its choice's name and its own, and its step from 0 to 63. Written behind the settings' fixed places, where a
///     link carries it, only where there is a genre: its name's length and its bytes, then how many options it weighs and
///     every one as its place among all the choices' options (<see cref="GenreChoices.All" />), in 10 bits, and its step,
///     in 6, one after another, six bits to a character.
/// </summary>
/// <param name="Name">How the page names it; empty for none.</param>
/// <param name="Options">Every option it weighs: a pool's above 0, every chance and value named.</param>
public sealed record GenreSettings(string Name, ImmutableArray<GenreSettings.Option> Options)
{
    /// <param name="Choice">The choice's name, such as "meter".</param>
    /// <param name="Name">The option's name, such as "3/4".</param>
    /// <param name="Step">Its amount, from 0 to 63.</param>
    public sealed record Option(string Choice, string Name, int Step);

    public static GenreSettings None { get; } = new("", []);

    /// <summary>Whether it is a genre: a name or an option it weighs.</summary>
    public bool IsGiven => Name.Length > 0 || !Options.IsEmpty;

    // every option of every choice, in the order of the choices and of their options, by which a link writes them
    private static readonly ImmutableArray<(string Choice, string Name)> Places =
        [..GenreChoices.All.SelectMany(choice => choice.Options.Select(option => (choice.Name, option.Name)))];

    private const int PlaceBits = 10;
    private const int StepBits = 6;
    private const int CountBits = 12;

    /// <summary>The longest name, in bytes of UTF-8, which its length's character holds.</summary>
    public const int MaxNameBytes = 63;

    /// <summary>The genre as a request gives it: every choice it names, and its options' steps.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>? ToRequest() => Options.IsEmpty
        ? null
        : Options.GroupBy(x => x.Choice).ToDictionary(x => x.Key, x => (IReadOnlyDictionary<string, int>)x.ToDictionary(y => y.Name, y => y.Step));

    /// <summary>Its options as the characters a link writes them in: how many, then every one's place and step.</summary>
    public IEnumerable<int> OptionCharacters()
    {
        if (Places.Length > 1 << PlaceBits)
            throw new InvalidOperationException("The choices have more options than a link writes.");

        var bits = new List<bool>();
        void Write(int value, int count)
        {
            if (value < 0 || value >= 1 << count)
                throw new InvalidOperationException($"{value} does not fit in {count} bits.");
            bits.AddRange(Enumerable.Range(0, count).Select(bit => (value >> bit & 1) == 1));
        }

        Write(Options.Length, CountBits);
        foreach (var option in Options)
        {
            var place = Places.IndexOf((option.Choice, option.Name));
            if (place < 0)
                throw new InvalidOperationException($"{option.Choice} has no option {option.Name}.");
            Write(place, PlaceBits);
            Write(option.Step, StepBits);
        }

        return Pack(bits);
    }

    /// <summary>The genre as the characters behind the settings' fixed places, none where there is no genre.</summary>
    public string Format()
    {
        if (!IsGiven)
            return "";

        var name = Encoding.UTF8.GetBytes(Name);
        if (name.Length > MaxNameBytes)
            throw new InvalidOperationException($"A genre's name is {MaxNameBytes} bytes at the most.");

        List<int> characters = [name.Length, ..Pack([..name.SelectMany(x => Enumerable.Range(0, 8).Select(bit => (x >> bit & 1) == 1))]), ..OptionCharacters()];
        return new string([..characters.Select(x => Base64.Digits[x])]);
    }

    /// <summary>A genre read from the characters behind the settings' fixed places, none for anything that is not one.</summary>
    public static GenreSettings? Parse(string text)
    {
        if (text.Length == 0)
            return None;

        var values = text.Select(x => Base64.Digits.IndexOf(x)).ToArray();
        if (values.Any(x => x < 0))
            return null;

        var nameBytes = values[0];
        var nameCharacters = (nameBytes * 8 + 5) / 6;
        if (values.Length < 1 + nameCharacters)
            return null;

        var nameBits = Unpack(values[1..(1 + nameCharacters)]);
        var name = Enumerable.Range(0, nameBytes).Select(i => (byte)Enumerable.Range(0, 8).Sum(bit => nameBits[i * 8 + bit] ? 1 << bit : 0)).ToArray();
        var bits = Unpack(values[(1 + nameCharacters)..]);
        var b = 0;
        int? Read(int count)
        {
            if (b + count > bits.Length)
                return null;
            var value = Enumerable.Range(0, count).Sum(bit => bits[b + bit] ? 1 << bit : 0);
            b += count;
            return value;
        }

        if (Read(CountBits) is not { } count)
            return null;
        var options = ImmutableArray.CreateBuilder<Option>();
        for (var i = 0; i < count; i++)
        {
            if (Read(PlaceBits) is not { } place || Read(StepBits) is not { } step || place >= Places.Length)
                return null;
            options.Add(new Option(Places[place].Choice, Places[place].Name, step));
        }

        // nothing but the last character's zeros past the options
        if ((bits.Length - b) >= 6 || bits[b..].Any(x => x))
            return null;
        return new GenreSettings(Encoding.UTF8.GetString(name), options.ToImmutable());
    }

    private static IEnumerable<int> Pack(IReadOnlyList<bool> bits)
    {
        for (var i = 0; i < bits.Count; i += 6)
            yield return bits.Skip(i).Take(6).Select((x, bit) => x ? 1 << bit : 0).Sum();
    }

    private static bool[] Unpack(IEnumerable<int> values) => [..values.SelectMany(x => Enumerable.Range(0, 6).Select(bit => (x >> bit & 1) == 1))];
}

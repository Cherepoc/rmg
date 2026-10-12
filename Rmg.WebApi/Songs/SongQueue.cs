using System.Net;

namespace Rmg.WebApi.Songs;

/// <summary>
///     The songs being made and those waiting their turn. As many are made at once as it is given, and each core that
///     comes free goes to the next client waiting, one song a client a round, so that a client asking for many delays
///     another by a song rather than by all of its own. A client may keep only so many waiting, and all of them together
///     only so many; where they are full, the newest of the longest line makes way for a client with fewer, so that a few
///     clients keeping theirs waiting shut nobody out, and the rest are turned away at once. A client that gives up while
///     waiting leaves its line.
/// </summary>
public sealed class SongQueue(int running, int waiting, int waitingPerClient)
{
    private readonly Lock _lock = new();

    // every client with a song waiting, by its line, and the lines in the order they take their turns
    private readonly Dictionary<string, Line> _lines = [];
    private readonly LinkedList<Line> _rotation = [];
    private int _running;
    private int _waiting;

    /// <summary>How many songs are being made and how many are waiting their turns, as of now.</summary>
    public (int Running, int Waiting) Load
    {
        get
        {
            lock (_lock) return (_running, _waiting);
        }
    }

    /// <summary>
    ///     A turn to make a song, or why there is none; one given is held until it is disposed, which hands it to the
    ///     next song waiting.
    /// </summary>
    public async Task<Turn> Enter(string client, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();

        Waiter waiter;
        lock (_lock)
        {
            if (_running < running)
            {
                _running++;
                return new Turn(this, null);
            }

            var own = _lines.GetValueOrDefault(client)?.Waiting.Count ?? 0;
            if (own >= waitingPerClient) return new Turn(null, Refusal.Client);
            if (_waiting >= waiting)
            {
                // the newest of the longest line turned away for one that would still wait fewer, else the newcomer
                var longest = _rotation.MaxBy(x => x.Waiting.Count)!;
                if (own + 1 >= longest.Waiting.Count) return new Turn(null, Refusal.Full);
                var displaced = longest.Waiting.Last!.Value;
                Remove(displaced);
                displaced.Turn.TrySetResult(new Turn(null, Refusal.Full));
            }

            if (!_lines.TryGetValue(client, out var line))
            {
                line = new Line(client);
                _lines[client] = line;
                line.InRotation = _rotation.AddLast(line);
            }

            waiter = new Waiter(line);
            waiter.InLine = line.Waiting.AddLast(waiter);
            _waiting++;
        }

        // outside the lock, as a token already cancelled calls back at once, and let go of once the turn is here, which
        // waits for a cancellation calling back
        using var registration = cancellation.Register(() => GiveUp(waiter, cancellation));
        return await waiter.Turn.Task;
    }

    private void GiveUp(Waiter waiter, CancellationToken cancellation)
    {
        lock (_lock)
        {
            // one given its turn already has it, and leaves it to be disposed
            if (waiter.InLine.List is null) return;

            Remove(waiter);
        }

        waiter.Turn.TrySetCanceled(cancellation);
    }

    private void Leave()
    {
        lock (_lock)
        {
            if (_rotation.First is not { } first)
            {
                _running--;
                return;
            }

            // the core goes straight to the next client's oldest, so nothing coming in can take it in between
            var waiter = first.Value.Waiting.First!.Value;
            Remove(waiter);
            if (_lines.ContainsKey(first.Value.Client))
            {
                _rotation.Remove(first);
                _rotation.AddLast(first);
            }

            waiter.Turn.TrySetResult(new Turn(this, null));
        }
    }

    private void Remove(Waiter waiter)
    {
        var line = waiter.Line;
        line.Waiting.Remove(waiter.InLine);
        _waiting--;
        if (line.Waiting.Count > 0) return;

        _lines.Remove(line.Client);
        _rotation.Remove(line.InRotation);
    }

    /// <summary>
    ///     Who a request is from, as far as a turn goes: its address, and for IPv6 the network it is on, as one home or
    ///     phone is given a whole /64 of addresses to change between.
    /// </summary>
    public static string ClientOf(IPAddress? address)
    {
        if (address is null) return "unknown";
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6) return address.ToString();

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"{new IPAddress(bytes)}/64";
    }

    /// <summary>Why a song was turned away: its client has as many waiting as it may, or the queue is full.</summary>
    public enum Refusal
    {
        Client,
        Full
    }

    public sealed class Turn(SongQueue? queue, Refusal? refusal) : IDisposable
    {
        private SongQueue? _queue = queue;

        /// <summary>Why there is no turn; null for one given.</summary>
        public Refusal? Refusal { get; } = refusal;

        public void Dispose() => Interlocked.Exchange(ref _queue, null)?.Leave();
    }

    private sealed class Line(string client)
    {
        public string Client { get; } = client;
        public LinkedList<Waiter> Waiting { get; } = [];
        public LinkedListNode<Line> InRotation { get; set; } = null!;
    }

    private sealed class Waiter(Line line)
    {
        public Line Line { get; } = line;
        public TaskCompletionSource<Turn> Turn { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public LinkedListNode<Waiter> InLine { get; set; } = null!;
    }
}

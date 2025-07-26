using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;

for (var i = 1; i <= 100; i++)
{
    var songPattern = SongGenerator.GenerateSong();
    var renderedSong = Render.RenderSong(songPattern);

    using var fileStream = File.Open(@$"D:\Projects\RMG\songs\song__{i}.mid", FileMode.Create);
    renderedSong.Write(fileStream);
    fileStream.Flush();
    Console.WriteLine($"Song {i} saved.");
}

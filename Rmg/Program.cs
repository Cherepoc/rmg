using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Rendering;

var songPattern = SongGenerator.GenerateSong();
var renderedSong = Render.RenderSong(songPattern);

using var fileStream = File.Open(@"D:\Projects\RMG\songs\song.mid", FileMode.Create);
renderedSong.Write(fileStream);
fileStream.Flush();

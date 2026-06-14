using Playlist;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace Playlist.UnitTests;

public class HltbCacheFileAccessTests
{
    [Fact]
    public void ReadTextAllowingWriter_reads_while_another_handle_has_write_share()
    {
        string path = Path.Combine(Path.GetTempPath(), "playlist-hltb-" + Guid.NewGuid() + ".json");
        const string payload = "{\"items\":[]}";
        File.WriteAllText(path, payload, Encoding.UTF8);

        using (var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read))
        {
            string read = HltbCacheFileAccess.ReadTextAllowingWriter(path);
            Assert.Equal(payload, read);
        }

        File.Delete(path);
    }

    [Fact]
    public void ReadTextAllowingWriter_reads_complete_payload_while_write_handle_stays_open()
    {
        string path = Path.Combine(Path.GetTempPath(), "playlist-hltb-" + Guid.NewGuid() + ".json");
        const string payload = "{\"items\":[{\"gameType\":0}]}";

        using (var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            writer.Write(bytes, 0, bytes.Length);
            writer.Flush();

            string read = HltbCacheFileAccess.ReadTextAllowingWriter(path);
            Assert.Equal(payload, read);
        }

        File.Delete(path);
    }
}

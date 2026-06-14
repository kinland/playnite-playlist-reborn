using System.IO;
using System.Text;

namespace Playlist
{
    /// <summary>
    /// Shared-read access to HLTB per-game cache JSON on disk.
    /// </summary>
    internal static class HltbCacheFileAccess
    {
        internal static FileStream OpenForSharedRead(string filePath)
        {
            return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        internal static string ReadTextAllowingWriter(string filePath)
        {
            using (FileStream stream = OpenForSharedRead(filePath))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }
}

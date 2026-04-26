using System;

namespace Playnite.SDK
{
    public interface IPlayniteAPI
    {
    }

    internal static class ResourceProvider
    {
        public static object GetResource(string key)
        {
            return null;
        }
    }
}

namespace Playnite.SDK.Models
{
    public class Game
    {
        public Guid Id { get; set; }
        public ulong Playtime { get; set; }
    }
}

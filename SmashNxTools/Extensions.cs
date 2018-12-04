using System.IO;

namespace SmashNxTools
{
    public static class Extensions
    {
        public static int ReadInt24(this BinaryReader reader)
        {
            var a = reader.ReadByte();
            var b = reader.ReadByte();
            var c = reader.ReadByte();

            return (c << 16) | (b << 8) | a;
        }
    }
}

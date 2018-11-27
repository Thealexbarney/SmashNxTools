using System.IO;

namespace SmashNxTools
{
    public class ArchiveHeader
    {
        public ulong Magic { get; set; }
        public long HeaderSize { get; set; }
        public long Field10 { get; set; }
        public long Field18 { get; set; }
        public long Field20 { get; set; }
        public long Field28 { get; set; }
        public long Field30 { get; set; }

        public ArchiveHeader(BinaryReader reader)
        {
            Magic = reader.ReadUInt64();
            HeaderSize = reader.ReadInt64();
            Field10 = reader.ReadInt64();
            Field18 = reader.ReadInt64();
            Field20 = reader.ReadInt64();
            Field28 = reader.ReadInt64();
            Field30 = reader.ReadInt64();
        }
    }
}

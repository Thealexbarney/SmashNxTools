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

    public class CompressedTableHeader
    {
        public int DataOffset { get; set; }
        public int Size { get; set; }
        public int SizeCompressed { get; set; }
        public int TotalSizeCompressed { get; set; }

        public CompressedTableHeader(BinaryReader reader)
        {
            DataOffset = reader.ReadInt32();
            Size = reader.ReadInt32();
            SizeCompressed = reader.ReadInt32();
            TotalSizeCompressed = reader.ReadInt32();
        }
    }
}

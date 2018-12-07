using System.Collections.Generic;
using System.IO;

namespace SmashNxTools
{
    public class Table20
    {
        public Table20Header Header { get; set; }

        public StreamRootTable[] StreamRoot { get; set; }
        public StreamHashToNameIndexTab[] StreamHashToNameIndex { get; set; }
        public StreamNameIndexTab[] StreamNameIndexToHash { get; set; }
        public StreamIndexToFileTab[] StreamIndexToFile { get; set; }
        public StreamFilesTab[] StreamFiles { get; set; }
        public Tab20F30[] Field30 { get; set; }
        public DirectoryListTab[] DirectoryList { get; set; }
        public DirectoryOffsetTable[] DirectoryOffsets { get; set; }
        public Tab20F18[] Field18 { get; set; }
        public FileListTab[] FileList { get; set; }
        public FileOffsetTab[] FileOffsets { get; set; }
        public DirectoryListLookupTab[] DirectoryListLookup { get; set; }
        public Tab20Fxx[] FieldXX { get; set; }
        public FileListLookupTab[] FileListLookup { get; set; }
        public Tab20F0CB[] Field0CB { get; set; }

        public Table20(BinaryReader reader)
        {
            Header = new Table20Header(reader);

            StreamRoot = new StreamRootTable[Header.Field34];
            for (int i = 0; i < Header.Field34; i++)
            {
                StreamRoot[i] = new StreamRootTable(reader);
            }

            StreamHashToNameIndex = new StreamHashToNameIndexTab[Header.Field38];
            for (int i = 0; i < Header.Field38; i++)
            {
                StreamHashToNameIndex[i] = new StreamHashToNameIndexTab(reader);
            }

            StreamNameIndexToHash = new StreamNameIndexTab[Header.Field38];
            for (int i = 0; i < Header.Field38; i++)
            {
                StreamNameIndexToHash[i] = new StreamNameIndexTab(reader);
            }

            StreamIndexToFile = new StreamIndexToFileTab[Header.Field3C];
            for (int i = 0; i < Header.Field3C; i++)
            {
                StreamIndexToFile[i] = new StreamIndexToFileTab(reader);
            }

            StreamFiles = new StreamFilesTab[Header.StreamFileCount];
            for (int i = 0; i < Header.StreamFileCount; i++)
            {
                StreamFiles[i] = new StreamFilesTab(reader);
            }

            Field30 = new Tab20F30[Header.Field30];
            for (int i = 0; i < Header.Field30; i++)
            {
                Field30[i] = new Tab20F30(reader);
            }

            DirectoryList = new DirectoryListTab[Header.Field4];
            for (int i = 0; i < Header.Field4; i++)
            {
                DirectoryList[i] = new DirectoryListTab(reader, i);
            }

            DirectoryOffsets = new DirectoryOffsetTable[Header.Field20 + Header.Field8];
            for (int i = 0; i < Header.Field20 + Header.Field8; i++)
            {
                DirectoryOffsets[i] = new DirectoryOffsetTable(reader, i);
            }

            Field18 = new Tab20F18[Header.Field18];
            for (int i = 0; i < Header.Field18; i++)
            {
                Field18[i] = new Tab20F18(reader);
            }

            FileList = new FileListTab[Header.FieldC];
            for (int i = 0; i < Header.FieldC; i++)
            {
                FileList[i] = new FileListTab(reader, i);
            }

            FileOffsets = new FileOffsetTab[Header.Field24 + Header.Field10];
            for (int i = 0; i < Header.Field24 + Header.Field10; i++)
            {
                FileOffsets[i] = new FileOffsetTab(reader, i);
            }

            DirectoryListLookup = new DirectoryListLookupTab[Header.Field4];
            for (int i = 0; i < Header.Field4; i++)
            {
                DirectoryListLookup[i] = new DirectoryListLookupTab(reader, i);
            }

            int fileCount = reader.ReadInt32();
            int groupCount = reader.ReadInt32();

            FieldXX = new Tab20Fxx[groupCount];
            for (int i = 0; i < groupCount; i++)
            {
                FieldXX[i] = new Tab20Fxx(reader);
            }

            FileListLookup = new FileListLookupTab[Header.Field14];
            for (int i = 0; i < Header.Field14; i++)
            {
                FileListLookup[i] = new FileListLookupTab(reader, i);
            }

            Field0CB = new Tab20F0CB[Header.FieldC];
            for (int i = 0; i < Header.FieldC; i++)
            {
                Field0CB[i] = new Tab20F0CB(reader);
            }

            HashSet<long> hashes = Hash.Hashes;

            foreach (var item in StreamRoot)
            {
                hashes.Add(item.Hash.GetHash());
            }

            foreach (var item in StreamHashToNameIndex)
            {
                hashes.Add(item.Hash.GetHash());
            }

            foreach (var item in StreamNameIndexToHash)
            {
                hashes.Add(item.Hash.GetHash());
            }

            foreach (var item in Field30)
            {
                hashes.Add(item.Hash.GetHash());
            }

            foreach (var item in DirectoryList)
            {
                hashes.Add(item.Path.GetHash());
                hashes.Add(item.Name.GetHash());
                hashes.Add(item.Parent.GetHash());
                hashes.Add(item.Hash4.GetHash());
            }

            foreach (var item in Field18)
            {
                hashes.Add(item.Hash.GetHash());
            }

            foreach (var item in FileList)
            {
                hashes.Add(item.Path.GetHash());
                hashes.Add(item.Extension.GetHash());
                hashes.Add(item.Parent.GetHash());
                hashes.Add(item.Name.GetHash());
            }

            foreach (var item in DirectoryListLookup)
            {
                hashes.Add(item.Hash.GetHash());
            }

            foreach (var item in FileListLookup)
            {
                hashes.Add(item.Hash.GetHash());
            }

            SetReferences();
        }

        public void SetReferences()
        {
            foreach (var item in StreamNameIndexToHash)
            {
                item.Stream = StreamIndexToFile[item.StreamIndex.Value];
            }

            foreach (var item in StreamIndexToFile)
            {
                item.File = StreamFiles[item.FileIndex];
            }

            foreach (var item in StreamHashToNameIndex)
            {
                item.Name = StreamNameIndexToHash[item.NameIndex.Value];
            }

            foreach (var item in FileList)
            {
                item.Directory = DirectoryList[item.DirectoryIndex.Value];
                item.FileOffset = FileOffsets[item.OffsetIndex];
            }

            foreach (var item in FileListLookup)
            {
                item.FileInfo = FileList[item.FileIndex.Value];
            }

            foreach (var item in DirectoryList)
            {
                item.DirOffset = DirectoryOffsets[item.DirOffsetIndex.Value];
                item.FirstFile = FileList[item.FirstFileIndex];
            }

            foreach (var item in FileOffsets)
            {
                if(item.Flag3)
                {
                    if (Header.Field10 + item.LinkFileIndex < FileOffsets.Length)
                    {
                        if (item.LinkFileIndex == 0x7d)
                        {
                            ;
                        }
                        item.LinkedOffset = FileOffsets[Header.Field10 + item.LinkFileIndex];

                        int i = Header.Field8;
                        while (DirectoryOffsets[i].FirstFileIndex + DirectoryOffsets[i].Field14 <= item.LinkFileIndex)
                        {
                            i++;
                        }

                        item.LinkedDirOffset = DirectoryOffsets[i];
                    }

                    //int i = Header.Field8;
                    //while (DirectoryOffsets[i].FirstFileIndex + DirectoryOffsets[i].Field14 < item.LinkFileIndex)
                    //{
                    //    i++;
                    //}

                    //item.LinkedDirOffset = DirectoryOffsets[i];
                    continue;
                }

                item.File = FileList[item.LinkFileIndex];
            }

            foreach (var item in DirectoryOffsets)
            {
                if (item.Field18 != 0xFFFFFF) item.LinkOffsetTable = DirectoryOffsets[item.Field18];
            }
        }
    }

    public class Table20Header
    {
        public int Length { get; set; }
        public int Field4 { get; set; }
        public int Field8 { get; set; }
        public int FieldC { get; set; }
        public int Field10 { get; set; }
        public int Field14 { get; set; }
        public int Field18 { get; set; }
        public int Field1C { get; set; }
        public int Field20 { get; set; }
        public int Field24 { get; set; }
        public int Field28 { get; set; }
        public int Field2C { get; set; }
        public byte Field30 { get; set; }
        public byte Field31 { get; set; }
        public short Field32 { get; set; }
        public int Field34 { get; set; }
        public int Field38 { get; set; }
        public int Field3C { get; set; }
        public int StreamFileCount { get; set; }

        public Table20Header(BinaryReader reader)
        {
            Length = reader.ReadInt32();
            Field4 = reader.ReadInt32();
            Field8 = reader.ReadInt32();
            FieldC = reader.ReadInt32();
            Field10 = reader.ReadInt32();
            Field14 = reader.ReadInt32();
            Field18 = reader.ReadInt32();
            Field1C = reader.ReadInt32();
            Field20 = reader.ReadInt32();
            Field24 = reader.ReadInt32();
            Field28 = reader.ReadInt32();
            Field2C = reader.ReadInt32();
            Field30 = reader.ReadByte();
            Field31 = reader.ReadByte();
            Field32 = reader.ReadInt16();
            Field34 = reader.ReadInt32();
            Field38 = reader.ReadInt32();
            Field3C = reader.ReadInt32();
            StreamFileCount = reader.ReadInt32();
        }
    }

    public class StreamRootTable
    {
        public Hash Hash;
        public Int24 Length;
        public int Start;

        public StreamRootTable(BinaryReader reader)
        {
            Hash = new Hash(reader);
            Length = new Int24(reader);
            Start = reader.ReadInt32();
        }
    }

    public class StreamHashToNameIndexTab
    {
        public Hash Hash;
        public Int24 NameIndex;

        public StreamNameIndexTab Name { get; set; }

        public StreamHashToNameIndexTab(BinaryReader reader)
        {
            Hash = new Hash(reader);
            NameIndex = new Int24(reader);
        }
    }

    public class StreamNameIndexTab
    {
        public Hash Hash;
        public Int24 StreamIndex;
        public int Field8;

        public StreamIndexToFileTab Stream { get; set; }

        public StreamNameIndexTab(BinaryReader reader)
        {
            Hash = new Hash(reader);
            StreamIndex = new Int24(reader);
            Field8 = reader.ReadInt32();
        }
    }

    public class StreamIndexToFileTab
    {
        public int FileIndex;
        public StreamFilesTab File { get; set; }

        public StreamIndexToFileTab(BinaryReader reader)
        {
            FileIndex = reader.ReadInt32();
        }
    }

    public class StreamFilesTab
    {
        public long Size;
        public long Offset;

        public StreamFilesTab(BinaryReader reader)
        {
            Size = reader.ReadInt64();
            Offset = reader.ReadInt64();
        }
    }

    public class Tab20F30
    {
        public int Field0;
        public Hash Hash;
        public Int24 Field9;

        public Tab20F30(BinaryReader reader)
        {
            Field0 = reader.ReadInt32();
            Hash = new Hash(reader);
            Field9 = new Int24(reader);
        }
    }

    public class DirectoryListTab
    {
        public Hash Path;
        public Int24 DirOffsetIndex;
        public Hash Name;
        public Hash Parent;
        public Hash Hash4;
        public int FirstFileIndex;
        public int Field24;
        public int Field28;
        public short Field2C;
        public short Field2E;
        public int Field30;

        public int Index { get; }
        public DirectoryOffsetTable DirOffset { get; set; }
        public FileListTab FirstFile { get; set; }

        public DirectoryListTab(BinaryReader reader, int index)
        {
            Path = new Hash(reader);
            DirOffsetIndex = new Int24(reader);
            Name = new Hash(reader);
            reader.BaseStream.Position += 3;
            Parent = new Hash(reader);
            reader.BaseStream.Position += 3;
            Hash4 = new Hash(reader);
            reader.BaseStream.Position += 3;
            FirstFileIndex = reader.ReadInt32();
            Field24 = reader.ReadInt32();
            Field28 = reader.ReadInt32();
            Field2C = reader.ReadInt16();
            Field2E = reader.ReadInt16();
            Field30 = reader.ReadInt32();
            Index = index;
        }

        public void AddFullHash()
        {
            if (Path.GetText() != null) return;

            string full = "";

            if (!AddField(Parent)) return;
            if (!AddField(Name)) return;

            Hash.AddHashIfExists(full);

            bool AddField(Hash hash)
            {
                if (hash.Crc == 0) return true;

                var text = hash.GetText();
                if (text == null) return false;

                if (full.Length > 0) full += "/";
                full += text;
                return true;
            }
        }
    }

    public class DirectoryOffsetTable
    {
        public long Offset;
        public int Size;
        public int SizeCompressed;
        public int FirstFileIndex;
        public int Field14;
        public int Field18;

        public int Index { get; }
        public DirectoryOffsetTable LinkOffsetTable { get; set; }

        public DirectoryOffsetTable(BinaryReader reader, int index)
        {
            Offset = reader.ReadInt64();
            Size = reader.ReadInt32();
            SizeCompressed = reader.ReadInt32();
            FirstFileIndex = reader.ReadInt32();
            Field14 = reader.ReadInt32();
            Field18 = reader.ReadInt32();
            Index = index;
        }
    }

    public class Tab20F18
    {
        public Hash Hash;
        public Int24 Field5;

        public Tab20F18(BinaryReader reader)
        {
            Hash = new Hash(reader);
            Field5 = new Int24(reader);
        }
    }

    public class FileListTab
    {
        public Hash Path;
        public Int24 DirectoryIndex;
        public Hash Extension;
        public Int24 FieldD;
        public Hash Parent;
        public Hash Name;
        public int OffsetIndex;
        public int Flags;

        public bool Flag1;
        public bool Flag9;
        public bool Flag17;
        public bool IsLink;
        public bool Flag21;

        public int Index { get; }
        public DirectoryListTab Directory { get; set; }
        public FileOffsetTab FileOffset { get; set; }

        public FileListTab(BinaryReader reader, int index)
        {
            Path = new Hash(reader);
            DirectoryIndex = new Int24(reader);
            Extension = new Hash(reader);
            FieldD = new Int24(reader);
            Parent = new Hash(reader);
            reader.BaseStream.Position += 3;
            Name = new Hash(reader);
            reader.BaseStream.Position += 3;
            OffsetIndex = reader.ReadInt32();
            Flags = reader.ReadInt32();

            Flag1 = (Flags & 2) != 0;
            Flag9 = (Flags & 0x200) != 0;
            Flag17 = (Flags & 0x20000) != 0;
            IsLink = (Flags & 0x100000) != 0;
            Flag21 = (Flags & 0x200000) != 0;
            Index = index;
        }

        public void AddFullHash()
        {
            if (Path.GetText() != null) return;

            string full = "";

            if (!AddField(Name)) return;
            if (!AddField(Parent)) return;
            if (!AddField(Extension)) return;

            Hash.AddHashIfExists(full);

            bool AddField(Hash hash)
            {
                if (hash.Crc == 0) return true;

                var text = hash.GetText();
                if (text == null) return false;

                if (full.Length > 0) full += "/";
                full += text;
                return true;
            }
        }
    }

    public class FileOffsetTab
    {
        public int Offset;
        public int SizeCompressed;
        public int Size;
        public int LinkFileIndex;
        public byte Flags;

        public bool IsCompressed;
        public bool Flag3;
        public bool Flag4;
        public bool Flag5;
        public bool Flag6;

        public int Index { get; }
        public FileListTab File { get; set; }
        public FileOffsetTab LinkedOffset { get; set; }
        public DirectoryOffsetTable LinkedDirOffset { get; set; }

        public FileOffsetTab(BinaryReader reader, int index)
        {
            Offset = reader.ReadInt32();
            SizeCompressed = reader.ReadInt32();
            Size = reader.ReadInt32();
            LinkFileIndex = reader.ReadInt24();
            Flags = reader.ReadByte();

            IsCompressed = (Flags & 3) == 3;
            Flag3 = (Flags & 8) != 0;
            Flag4 = (Flags & 0x10) != 0;
            Flag5 = (Flags & 0x20) != 0;
            Flag6 = (Flags & 0x40) != 0;
            Index = index;
        }
    }

    public class DirectoryListLookupTab
    {
        public Hash Hash;
        public Int24 Field5;

        public int Index { get; }
        public DirectoryListLookupTab(BinaryReader reader, int index)
        {
            Hash = new Hash(reader);
            Field5 = new Int24(reader);
            Index = index;
        }
    }

    public class Tab20Fxx
    {
        public int FileIndex;
        public int Count;

        public Tab20Fxx(BinaryReader reader)
        {
            FileIndex = reader.ReadInt32();
            Count = reader.ReadInt32();
        }
    }

    public class FileListLookupTab
    {
        public Hash Hash;
        public Int24 FileIndex;

        public int Index { get; }
        public FileListTab FileInfo { get; set; }

        public FileListLookupTab(BinaryReader reader, int index)
        {
            Hash = new Hash(reader);
            FileIndex = new Int24(reader);
            Index = index;
        }
    }

    public class Tab20F0CB
    {
        public int Field0;
        public int Field4;

        public Tab20F0CB(BinaryReader reader)
        {
            Field0 = reader.ReadInt32();
            Field4 = reader.ReadInt32();
        }
    }
}

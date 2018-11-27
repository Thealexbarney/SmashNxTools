using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace SmashNxTools
{
    public class Table20
    {
        public byte[] Data { get; set; }
        public Table20Header Header { get; set; }

        public StreamRootTable[] StreamRoot { get; set; }
        public StreamHashToIndexTab[] StreamHashToIndex { get; set; }
        public StreamIndexToHashTab[] StreamIndexToHash { get; set; }
        public StreamIndexToFileTab[] StreamIndexToFile { get; set; }
        public StreamFilesTab[] StreamFiles { get; set; }
        public Tab20F30[] Field30 { get; set; }
        public Tab20F04A[] Field04A { get; set; }
        public Tab20F20[] Field20 { get; set; }
        public Tab20F18[] Field18 { get; set; }
        public FilePathCombineTab[] FilePathCombine { get; set; }
        public Tab20F24[] Field24 { get; set; }
        public Tab20F04B[] Field04B { get; set; }
        public Tab20Fxx[] FieldXX { get; set; }
        public FileHashToIndexTab[] FileHashToIndex { get; set; }
        public Tab20F0CB[] Field0CB { get; set; }

        public Table20(BinaryReader reader)
        {
            long start = reader.BaseStream.Position;

            Header = new Table20Header(reader);

            reader.BaseStream.Position = start;
            Data = reader.ReadBytes(Header.Length);

            int pos = 0x44;

            StreamRoot = MemoryMarshal.Cast<byte, StreamRootTable>(Data.AsSpan(pos)).Slice(0, Header.Field34).ToArray();
            pos += StreamRoot.Length * Marshal.SizeOf<StreamRootTable>();

            StreamHashToIndex = MemoryMarshal.Cast<byte, StreamHashToIndexTab>(Data.AsSpan(pos)).Slice(0, Header.Field38).ToArray();
            pos += StreamHashToIndex.Length * Marshal.SizeOf<StreamHashToIndexTab>();

            StreamIndexToHash = MemoryMarshal.Cast<byte, StreamIndexToHashTab>(Data.AsSpan(pos)).Slice(0, Header.Field38).ToArray();
            pos += StreamIndexToHash.Length * Marshal.SizeOf<StreamIndexToHashTab>();

            StreamIndexToFile = MemoryMarshal.Cast<byte, StreamIndexToFileTab>(Data.AsSpan(pos)).Slice(0, Header.Field3C).ToArray();
            pos += StreamIndexToFile.Length * Marshal.SizeOf<StreamIndexToFileTab>();

            StreamFiles = MemoryMarshal.Cast<byte, StreamFilesTab>(Data.AsSpan(pos)).Slice(0, Header.StreamFileCount).ToArray();
            pos += StreamFiles.Length * Marshal.SizeOf<StreamFilesTab>();

            Field30 = MemoryMarshal.Cast<byte, Tab20F30>(Data.AsSpan(pos)).Slice(0, Header.Field30).ToArray();
            pos += Field30.Length * Marshal.SizeOf<Tab20F30>();

            Field04A = MemoryMarshal.Cast<byte, Tab20F04A>(Data.AsSpan(pos)).Slice(0, Header.Field4).ToArray();
            pos += Field04A.Length * Marshal.SizeOf<Tab20F04A>();

            Field20 = MemoryMarshal.Cast<byte, Tab20F20>(Data.AsSpan(pos)).Slice(0, Header.Field20 + Header.Field8).ToArray();
            pos += Field20.Length * Marshal.SizeOf<Tab20F20>();

            Field18 = MemoryMarshal.Cast<byte, Tab20F18>(Data.AsSpan(pos)).Slice(0, Header.Field18).ToArray();
            pos += Field18.Length * Marshal.SizeOf<Tab20F18>();

            FilePathCombine = MemoryMarshal.Cast<byte, FilePathCombineTab>(Data.AsSpan(pos)).Slice(0, Header.FieldC).ToArray();
            pos += FilePathCombine.Length * Marshal.SizeOf<FilePathCombineTab>();

            Field24 = MemoryMarshal.Cast<byte, Tab20F24>(Data.AsSpan(pos)).Slice(0, Header.Field24 + Header.Field10).ToArray();
            pos += Field24.Length * Marshal.SizeOf<Tab20F24>();

            Field04B = MemoryMarshal.Cast<byte, Tab20F04B>(Data.AsSpan(pos)).Slice(0, Header.Field4).ToArray();
            pos += Field04B.Length * Marshal.SizeOf<Tab20F04B>();

            int fileCount = BitConverter.ToInt32(Data, pos);
            int groupCount = BitConverter.ToInt32(Data, pos + 4);
            pos += 8;

            FieldXX = MemoryMarshal.Cast<byte, Tab20Fxx>(Data.AsSpan(pos)).Slice(0, groupCount).ToArray();
            pos += FieldXX.Length * Marshal.SizeOf<Tab20Fxx>();

            FileHashToIndex = MemoryMarshal.Cast<byte, FileHashToIndexTab>(Data.AsSpan(pos)).Slice(0, Header.Field14).ToArray();
            pos += FileHashToIndex.Length * Marshal.SizeOf<FileHashToIndexTab>();

            Field0CB = MemoryMarshal.Cast<byte, Tab20F0CB>(Data.AsSpan(pos)).Slice(0, Header.FieldC).ToArray();

            HashSet<long> hashes = Hash.Hashes;

            foreach (var item in StreamRoot)
            {
                hashes.Add(item.Hash.GetHash());
            }

            foreach (var item in StreamHashToIndex)
            {
                hashes.Add(item.Hash.GetHash());
            }

            foreach (var item in StreamIndexToHash)
            {
                hashes.Add(item.Hash.GetHash());
            }

            foreach (var item in Field30)
            {
                hashes.Add(item.Hash.GetHash());
            }

            foreach (var item in Field04A)
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

            foreach (var item in FilePathCombine)
            {
                hashes.Add(item.Path.GetHash());
                hashes.Add(item.Extension.GetHash());
                hashes.Add(item.Parent.GetHash());
                hashes.Add(item.Name.GetHash());
            }

            foreach (var item in Field04B)
            {
                hashes.Add(item.Hash.GetHash());
            }

            foreach (var item in FileHashToIndex)
            {
                hashes.Add(item.Hash.GetHash());
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

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StreamRootTable
    {
        public Hash Hash;
        public Int24 Length;
        public int Start;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StreamHashToIndexTab
    {
        public Hash Hash;
        public Int24 StreamIndex;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StreamIndexToHashTab
    {
        public Hash Hash;
        public Int24 SongIndex;
        public int Field8;
    }

    public struct StreamIndexToFileTab
    {
        public int FileIndex;
    }

    public struct StreamFilesTab
    {
        public long Size;
        public long Offset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Tab20F30
    {
        public int Field0;
        public Hash Hash;
        public Int24 Field9;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Tab20F04A
    {
        public Hash Path;
        public Int24 Field5;
        public Hash Name;
        public Int24 FieldD;
        public Hash Parent;
        public Int24 Field15;
        public Hash Hash4;
        public Int24 Field1D;
        public int Field20;
        public int Field24;
        public int Field28;
        public short Field2C;
        public short Field2E;
        public int Field30;

        public void AddFullHash()
        {
            if (Path.GetText() != null) return;

            string full = "";

            if (!AddField(Hash4)) return;
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

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Tab20F20
    {
        public long Offset;
        public int Field8;
        public int FieldC;
        public int Field10;
        public int Field14;
        public int Field18;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Tab20F18
    {
        public Hash Hash;
        public Int24 Field5;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FilePathCombineTab
    {
        public Hash Path;
        public Int24 Field5;
        public Hash Extension;
        public Int24 FieldD;
        public Hash Parent;
        public Int24 Field15;
        public Hash Name;
        public Int24 Field1D;
        public int Field20;
        public int Field24;

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

    public struct Tab20F24
    {
        public int Field0;
        public int Field4;
        public int Field8;
        public int FieldC;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Tab20F04B
    {
        public Hash Hash;
        public Int24 Field5;
    }

    public struct Tab20Fxx
    {
        public int FileIndex;
        public int Count;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FileHashToIndexTab
    {
        public Hash Hash;
        public Int24 Field5;
    }

    public struct Tab20F0CB
    {
        public int Field0;
        public int Field4;
    }
}

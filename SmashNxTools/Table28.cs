using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace SmashNxTools
{
    public class Table28
    {
        public byte[] Data { get; set; }
        public Table28Header Header { get; set; }

        public DirectoryHashToIndexTable[] DirectoryHashToIndex { get; set; }
        public DirectoryChildrenTable[] DirectoryChildren { get; set; }
        public Tab28F10A[] Field10A { get; set; }
        public Tab28F0C[] Field0C { get; set; }
        public Tab28F10B[] Field10B { get; set; }

        public Table28(BinaryReader reader)
        {
            long start = reader.BaseStream.Position;

            Header = new Table28Header(reader);

            reader.BaseStream.Position = start;
            Data = reader.ReadBytes(Header.Length);

            int pos = 0x14;

            DirectoryHashToIndex = MemoryMarshal.Cast<byte, DirectoryHashToIndexTable>(Data.AsSpan(pos)).Slice(0, Header.Field8).ToArray();
            pos += DirectoryHashToIndex.Length * Marshal.SizeOf<DirectoryHashToIndexTable>();

            DirectoryChildren = MemoryMarshal.Cast<byte, DirectoryChildrenTable>(Data.AsSpan(pos)).Slice(0, Header.Field8).ToArray();
            pos += DirectoryChildren.Length * Marshal.SizeOf<DirectoryChildrenTable>();

            Field10A = MemoryMarshal.Cast<byte, Tab28F10A>(Data.AsSpan(pos)).Slice(0, Header.Field10).ToArray();
            pos += Field10A.Length * Marshal.SizeOf<Tab28F10A>();

            Field0C = MemoryMarshal.Cast<byte, Tab28F0C>(Data.AsSpan(pos)).Slice(0, Header.FieldC).ToArray();
            pos += Field0C.Length * Marshal.SizeOf<Tab28F0C>();

            Field10B = MemoryMarshal.Cast<byte, Tab28F10B>(Data.AsSpan(pos)).Slice(0, Header.Field10).ToArray();

            HashSet<long> hashes = Hash.Hashes;

            foreach (var item in DirectoryHashToIndex)
            {
                hashes.Add(item.Hash.GetHash());
            }

            foreach (var item in DirectoryChildren)
            {
                hashes.Add(item.Path.GetHash());
                hashes.Add(item.ParentDir.GetHash());
                hashes.Add(item.Name.GetHash());
            }

            foreach (var item in Field10A)
            {
                hashes.Add(item.Hash.GetHash());
            }

            foreach (var item in Field10B)
            {
                hashes.Add(item.Path.GetHash());
                hashes.Add(item.ParentDirectory.GetHash());
                hashes.Add(item.Name.GetHash());
                hashes.Add(item.Extension.GetHash());
            }
        }
    }

    public class Table28Header
    {
        public int Length { get; set; }
        public int Field4 { get; set; }
        public int Field8 { get; set; }
        public int FieldC { get; set; }
        public int Field10 { get; set; }

        public Table28Header(BinaryReader reader)
        {
            Length = reader.ReadInt32();
            Field4 = reader.ReadInt32();
            Field8 = reader.ReadInt32();
            FieldC = reader.ReadInt32();
            Field10 = reader.ReadInt32();
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DirectoryHashToIndexTable
    {
        public Hash Hash;
        public Int24 Field5;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DirectoryChildrenTable
    {
        public Hash Path;
        public Int24 ChildDirCount;
        public Hash ParentDir;
        public Int24 ChildFileCount;
        public Hash Name;
        public Int24 Field15;
        public int EntryStartIndex;
        public int EntryCount;
        
        public void AddFullHash()
        {
            if (Path.GetText() != null) return;

            string full = "";

            if (!AddField(ParentDir)) return;
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
    public struct Tab28F10A
    {
        public Hash Hash;
        public Int24 Field5;
    }

    public struct Tab28F0C
    {
        public int Field0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Tab28F10B
    {
        public Hash Path;
        public Int24 NextSibling;
        public Hash ParentDirectory;
        public Int24 Type;
        public Hash Name;
        public Int24 Field15;
        public Hash Extension;
        public Int24 Field1D;

        public void AddFullHash()
        {
            if (Path.GetText() != null) return;

            string full = "";

            if (!AddField(ParentDirectory)) return;
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
}

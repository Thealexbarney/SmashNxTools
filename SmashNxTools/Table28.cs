using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SmashNxTools
{
    public class Table28
    {
        public Table28Header Header { get; set; }

        public DirectoryListLookupTab2[] DirectoryListLookup { get; set; }
        public DirectoryListTab2[] DirectoryList { get; set; }
        public EntryListLookupTab[] EntryListLookup { get; set; }
        public Tab28F0C[] Field0C { get; set; }
        public EntryListTab[] EntryList { get; set; }

        public Table28(BinaryReader reader)
        {
            Header = new Table28Header(reader);

            DirectoryListLookup = new DirectoryListLookupTab2[Header.DirectoryCount];
            for (int i = 0; i < Header.DirectoryCount; i++)
            {
                DirectoryListLookup[i] = new DirectoryListLookupTab2(reader);
            }

            DirectoryList = new DirectoryListTab2[Header.DirectoryCount];
            for (int i = 0; i < Header.DirectoryCount; i++)
            {
                DirectoryList[i] = new DirectoryListTab2(reader, i);
            }

            EntryListLookup = new EntryListLookupTab[Header.EntryCount];
            for (int i = 0; i < Header.EntryCount; i++)
            {
                EntryListLookup[i] = new EntryListLookupTab(reader);
            }

            Field0C = new Tab28F0C[Header.FieldC];
            for (int i = 0; i < Header.FieldC; i++)
            {
                Field0C[i] = new Tab28F0C(reader);
            }

            EntryList = new EntryListTab[Header.EntryCount];
            for (int i = 0; i < Header.EntryCount; i++)
            {
                EntryList[i] = new EntryListTab(reader, i);
            }

            HashSet<long> hashes = Hash.Hashes;

            foreach (var item in DirectoryListLookup)
            {
                hashes.Add(item.Hash.GetHash());
            }

            foreach (var item in DirectoryList)
            {
                hashes.Add(item.Path.GetHash());
                hashes.Add(item.Parent.GetHash());
                hashes.Add(item.Name.GetHash());
            }

            foreach (var item in EntryListLookup)
            {
                hashes.Add(item.Hash.GetHash());
            }

            foreach (var item in EntryList)
            {
                hashes.Add(item.Path.GetHash());
                hashes.Add(item.Parent.GetHash());
                hashes.Add(item.Name.GetHash());
                hashes.Add(item.Extension.GetHash());
            }

            SetReferences();
        }

        public void SetReferences()
        {
            foreach (var item in DirectoryListLookup)
            {
                item.Directory = DirectoryList[item.DirectoryIndex.Value];
            }

            foreach (var item in DirectoryList)
            {
                item.FirstEntry = EntryList[item.EntryStartIndex];
            }

            foreach (var item in EntryListLookup)
            {
                item.Entry = EntryList[item.EntryIndex.Value];
            }

            foreach (var item in EntryList.Where(x => x.NextSiblingIndex != 0xFFFFFF))
            {
                item.NextSibling = EntryList[item.NextSiblingIndex];
            }
        }
    }

    public class Table28Header
    {
        public int Length { get; set; }
        public int Field4 { get; set; }
        public int DirectoryCount { get; set; }
        public int FieldC { get; set; }
        public int EntryCount { get; set; }

        public Table28Header(BinaryReader reader)
        {
            Length = reader.ReadInt32();
            Field4 = reader.ReadInt32();
            DirectoryCount = reader.ReadInt32();
            FieldC = reader.ReadInt32();
            EntryCount = reader.ReadInt32();
        }
    }

    public class DirectoryListLookupTab2
    {
        public Hash Hash;
        public Int24 DirectoryIndex;

        public DirectoryListTab2 Directory { get; set; }

        public DirectoryListLookupTab2(BinaryReader reader)
        {
            Hash = new Hash(reader);
            DirectoryIndex = new Int24(reader);
        }
    }

    public class DirectoryListTab2
    {
        public Hash Path;
        public int ChildDirCount;
        public Hash Parent;
        public int ChildFileCount;
        public Hash Name;
        public int EntryStartIndex;
        public int EntryCount;

        public EntryListTab FirstEntry { get; set; }
        public int Index { get; set; }

        public DirectoryListTab2(BinaryReader reader, int index)
        {
            Index = index;
            Path = new Hash(reader);
            ChildDirCount = reader.ReadInt24();
            Parent = new Hash(reader);
            ChildFileCount = reader.ReadInt24();
            Name = new Hash(reader);
            reader.BaseStream.Position += 3;
            EntryStartIndex = reader.ReadInt32();
            EntryCount = reader.ReadInt32();
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

    public class EntryListLookupTab
    {
        public Hash Hash;
        public Int24 EntryIndex;

        public EntryListTab Entry { get; set; }

        public EntryListLookupTab(BinaryReader reader)
        {
            Hash = new Hash(reader);
            EntryIndex = new Int24(reader);
        }
    }

    public class Tab28F0C
    {
        public int Field0;

        public Tab28F0C(BinaryReader reader)
        {
            Field0 = reader.ReadInt32();
        }
    }

    public class EntryListTab
    {
        public Hash Path;
        public int NextSiblingIndex;
        public Hash Parent;
        public int Type;
        public Hash Name;
        public Hash Extension;

        public int Index { get; set; }
        public EntryListTab NextSibling { get; set; }

        public EntryListTab(BinaryReader reader, int index)
        {
            Index = index;
            Path = new Hash(reader);
            NextSiblingIndex = reader.ReadInt24();
            Parent = new Hash(reader);
            Type = reader.ReadInt24();
            Name = new Hash(reader);
            reader.BaseStream.Position += 3;
            Extension = new Hash(reader);
            reader.BaseStream.Position += 3;
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
}

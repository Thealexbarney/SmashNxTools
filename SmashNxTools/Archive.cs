using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Zstandard.Net;

namespace SmashNxTools
{
    public class Archive
    {
        public ArchiveHeader Header { get; set; }
        public Table20 Table20 { get; set; }
        public Table28 Table28 { get; set; }
        private Stream Stream { get; }

        public Archive(Stream stream)
        {
            Stream = stream;
            var reader = new BinaryReader(stream);

            Header = new ArchiveHeader(reader);

            (byte[] table20, byte[] table28) = GetTables();

            Table20 = new Table20(new BinaryReader(new MemoryStream(table20)));
            Table28 = new Table28(new BinaryReader(new MemoryStream(table28)));
        }

        private (byte[] table20, byte[] table28) GetTables()
        {
            var reader = new BinaryReader(Stream);

            reader.BaseStream.Position = Header.Field20;

            if (reader.ReadInt32() < 0x100) // heuristic for detecting compressed tables
            {
                reader.BaseStream.Position = Header.Field20;
                var header20 = new CompressedTableHeader(reader);
                var table20 = new byte[header20.Size];

                reader.BaseStream.Position = Header.Field28;
                var header28 = new CompressedTableHeader(reader);
                var table28 = new byte[header28.Size];

                reader.BaseStream.Position = Header.Field20 + header20.DataOffset;

                using (var compStream = new ZstandardStream(Stream, CompressionMode.Decompress, true))
                {
                    compStream.Read(table20, 0, table20.Length);
                }

                reader.BaseStream.Position = Header.Field28 + header28.DataOffset;

                using (var compStream = new ZstandardStream(Stream, CompressionMode.Decompress, true))
                {
                    compStream.Read(table28, 0, table28.Length);
                }

                return (table20, table28);
            }
            else
            {
                reader.BaseStream.Position = Header.Field20;
                int table20Length = reader.ReadInt32();

                reader.BaseStream.Position = Header.Field20;
                byte[] table20 = reader.ReadBytes(table20Length);

                reader.BaseStream.Position = Header.Field28;
                int table28Length = reader.ReadInt32();

                reader.BaseStream.Position = Header.Field28;
                byte[] table28 = reader.ReadBytes(table28Length);

                return (table20, table28);
            }
        }

        public void FindFullHashes()
        {
            foreach (var hash in Table20.DirectoryList)
            {
                hash.AddFullHash();
            }

            foreach (var hash in Table20.FileList)
            {
                hash.AddFullHash();
            }

            foreach (var hash in Table28.DirectoryList)
            {
                hash.AddFullHash();
            }

            foreach (var hash in Table28.EntryList)
            {
                hash.AddFullHash();
            }
        }

        public void ExtractStreams(string dir)
        {
            for (int i = 0; i < Table20.StreamNameIndexToHash.Length; i++)
            {
                var name = Table20.StreamNameIndexToHash[i].Hash.GetText();
                var fileInfo = Table20.StreamNameIndexToHash[i].Stream.File;
                var fileIndex = Table20.StreamNameIndexToHash[i].Stream.FileIndex;
                if (name == null)
                {
                    name = fileIndex.ToString("D4");
                    if (i > 0x92a) name += ".webm";
                }
                name = name.Replace("stream:/", "");

                var path = Path.Combine(dir, name);

                Directory.CreateDirectory(Path.GetDirectoryName(path));

                using (var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
                {
                    Stream.Position = fileInfo.Offset;
                    Stream.CopyStream(file, fileInfo.Size);
                }
            }
        }

        public void ExtractFile(string filename, string outDir, IProgressReport progress)
        {
            var file = Table20.FileList.FirstOrDefault(x => x.Path.GetText() == filename);
            if (file == null) return;

            ExtractFileIndex(file.Index, outDir, progress);
        }

        public byte[] GetFileFromIndex(int index)
        {
            FileListTab file = Table20.FileList[index];
            string name = file.Path.GetText();
            DirectoryListTab dir = file.Directory;
            DirectoryOffsetTable dirOffset = dir.DirOffset;
            FileOffsetTab offsetInfo = file.FileOffset;

            bool isLink = false;

            if (file.IsLink)
            {
                isLink = true;

                while (file.IsLink)
                {
                    file = file.FileOffset.File;
                }

                dir = file.Directory;
                dirOffset = dir.DirOffset;
                offsetInfo = file.FileOffset;
            }

            if (offsetInfo.Flag3)
            {
                dirOffset = offsetInfo.LinkedDirOffset;
                offsetInfo = offsetInfo.LinkedOffset;
            }

            if (isLink) return new byte[0];
            if (offsetInfo.Size == 0) return new byte[0];

            long offset = Header.Field10 + dirOffset.Offset + offsetInfo.Offset * 4;

            var data = new byte[offsetInfo.Size];
            Stream.Position = offset;

            if (offsetInfo.SizeCompressed == 0 || offsetInfo.SizeCompressed == offsetInfo.Size)
            {
                Stream.Read(data, 0, offsetInfo.Size);
            }
            else
            {
                using (var compStream = new ZstandardStream(Stream, CompressionMode.Decompress, true))
                {
                    compStream.Read(data, 0, offsetInfo.Size);
                }
            }

            return data;
        }

        public void ExtractFileIndex(int index, string outDir, IProgressReport progress, StringBuilder sb = null)
        {
            FileListTab file = Table20.FileList[index];
            string name = file.Path.GetText();
            DirectoryListTab dir = file.Directory;
            DirectoryOffsetTable dirOffset = dir.DirOffset;
            FileOffsetTab offsetInfo = file.FileOffset;

            bool isLink = false;

            long offset = Header.Field10 + dirOffset.Offset + file.FileOffset.Offset * 4;
            string path;

            if (name != null)
            {
                path = Path.Combine(outDir, name);
            }
            else if (file.Parent.HasText())
            {
                path = Path.Combine(outDir, file.Parent.GetText(), index.ToString());
            }
            else
            {
                path = Path.Combine(outDir, "_", index.ToString());
            }

            if (file.IsLink)
            {
                isLink = true;

                while (file.IsLink)
                {
                    file = file.FileOffset.File;
                }

                dir = file.Directory;
                dirOffset = dir.DirOffset;
                offsetInfo = file.FileOffset;

                offset = Header.Field10 + dirOffset.Offset + offsetInfo.Offset * 4;
            }

            if (offsetInfo.Flag3)
            {
                dirOffset = offsetInfo.LinkedDirOffset;
                offsetInfo = offsetInfo.LinkedOffset;

                offset = Header.Field10 + dirOffset.Offset + offsetInfo.Offset * 4;
            }

            //sb?.AppendLine($"{name}, 0x{file.Flags:x2}, 0x{dirOffset.Offset:x}, 0x{file.FileOffset.Offset:x}, 0x{offset:x}, 0x{file.FileOffset.SizeCompressed:x}, 0x{file.FileOffset.Size:x}, 0x{file.FileOffset.Flags:x2}, 0x{file.FileOffset.LinkFileIndex:x}, {file.Flag1}, {file.Flag9}, {file.Flag17}, {file.IsLink}, {file.Flag21}, {file.FileOffset.IsCompressed}, {file.FileOffset.Flag3}, {file.FileOffset.Flag4}, {file.FileOffset.Flag5}, {file.FileOffset.Flag6},");

            //return;

            try
            {
                //if (isLink) return;
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                using (var fileOut = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
                //using (var fileOut = Stream.Null)
                {
                    Stream.Position = offset;

                    if (offsetInfo.Size == 0) return;

                    if (offsetInfo.SizeCompressed == 0 || offsetInfo.SizeCompressed == offsetInfo.Size)
                    {
                        Stream.CopyStream(fileOut, offsetInfo.Size);
                    }
                    else
                    {
                        using (var compStream = new ZstandardStream(Stream, CompressionMode.Decompress, true))
                        {
                            compStream.CopyStream(fileOut, offsetInfo.Size);
                        }
                    }
                }

                sb?.AppendLine($"{name}, 0x{file.Flags:x2}, 0x{dirOffset.Offset:x}, 0x{offsetInfo.Offset:x}, 0x{offset:x}, 0x{offsetInfo.SizeCompressed:x}, 0x{offsetInfo.Size:x}, 0x{offsetInfo.Flags:x2}, 0x{offsetInfo.LinkFileIndex:x}, {file.Flag1}, {file.Flag9}, {file.Flag17}, {file.IsLink}, {file.Flag21}, {offsetInfo.IsCompressed}, {offsetInfo.Flag3}, {offsetInfo.Flag4}, {offsetInfo.Flag5}, {offsetInfo.Flag6}, ");
            }
            catch (InvalidDataException)
            {
                progress?.LogMessage($"File index 0x{file.Index:x5} Offset 0x{offset:x9}: Can't decompress {path}");
                try
                {
                    File.Delete(path);
                    sb?.AppendLine($"{name}, 0x{file.Flags:x2}, 0x{dirOffset.Offset:x}, 0x{offsetInfo.Offset:x}, 0x{offset:x}, 0x{offsetInfo.SizeCompressed:x}, 0x{offsetInfo.Size:x}, 0x{offsetInfo.Flags:x2}, 0x{offsetInfo.LinkFileIndex:x}, {file.Flag1}, {file.Flag9}, {file.Flag17}, {file.IsLink}, {file.Flag21}, {offsetInfo.IsCompressed}, {offsetInfo.Flag3}, {offsetInfo.Flag4}, {offsetInfo.Flag5}, {offsetInfo.Flag6}, X");

                    var badPath = Path.Combine(outDir, "bad", index.ToString());
                    Directory.CreateDirectory(Path.GetDirectoryName(badPath));

                    using (var fileOut = new FileStream(badPath, FileMode.Create, FileAccess.ReadWrite))
                    {
                        Stream.Position = offset;
                        var info = file.FileOffset;

                        if (info.Size == 0) return;

                        Stream.CopyStream(fileOut, info.Size);
                    }
                }
                catch (Exception) { }
            }
            catch (Exception)
            {
                progress?.LogMessage($"File index 0x{file.Index:x5}: Bad path {path}");
                try
                {
                    File.Delete(path);
                }
                catch (Exception) { }
            }
        }

        public void ExtractDirs(string outDir, IProgressReport progress = null)
        {

        }

        public void ExtractFiles(string outDir, IProgressReport progress = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("name, file flags, dir offset, file offset, offset, size comp, size, file offset flags, offsetToFile, F1, F9, F17, Is link, F21, Is comp, OF3, OF4, OF5, OF6, bad");

            FileListTab[] fileListTabs = Table20.FileList;

            progress?.SetTotal(fileListTabs.Length);

            for (int i = 0; i < fileListTabs.Length; i++)
            {
                //ExtractFileIndex(i, outDir, progress, sb);
                ExtractFileIndex(i, outDir, progress);
                progress?.ReportAdd(1);
            }

            //File.WriteAllText("list2.csv", sb.ToString());
        }

        public IEnumerable<byte[]> EnumerateFiles(IProgressReport progress = null)
        {
            FileListTab[] fileListTabs = Table20.FileList;

            progress?.SetTotal(fileListTabs.Length);

            for (int i = 0; i < fileListTabs.Length; i++)
            {
                bool success = false;
                byte[] data = new byte[0];

                try
                {
                    data = GetFileFromIndex(i);
                    success = true;
                }
                catch (Exception)
                {
                    progress?.LogMessage($"Error getting file {i}");
                }

                if (success) yield return data;
                progress?.ReportAdd(1);
            }
        }

        public void Print(string dir)
        {
            Directory.CreateDirectory(dir);

            File.WriteAllText(Path.Combine(dir, "StreamRoot.txt"), PrintStruct(Table20.StreamRoot));
            File.WriteAllText(Path.Combine(dir, "StreamHashToNameIndex.txt"), PrintStruct(Table20.StreamHashToNameIndex)); // count 0x97f
            File.WriteAllText(Path.Combine(dir, "StreamNameIndexToHash.txt"), PrintStruct(Table20.StreamNameIndexToHash)); // max 0x14f3, count 0x97f
            File.WriteAllText(Path.Combine(dir, "StreamIndexToFileIndex.txt"), PrintStruct(Table20.StreamIndexToFile)); // max 0xaa0, count 0x1500
            File.WriteAllText(Path.Combine(dir, "StreamFileOffsets.txt"), PrintStruct(Table20.StreamFiles)); //count 0xaa0
            File.WriteAllText(Path.Combine(dir, "table20_30.txt"), PrintStruct(Table20.Field30)); // Count 0xd
            File.WriteAllText(Path.Combine(dir, "DirectoryList.txt"), PrintStruct(Table20.DirectoryList)); //FolderPathTable Max 0x73bb1/0x64d1, count 0x64ed
            File.WriteAllText(Path.Combine(dir, "DirectoryOffsets.txt"), PrintStruct(Table20.DirectoryOffsets)); // Directory offset table  Max 0c860c8/0x507/0x8f82, count 0x8f82
            File.WriteAllText(Path.Combine(dir, "table20_18.txt"), PrintStruct(Table20.Field18)); //Some dir list Max 0, count 0x64d0
            File.WriteAllText(Path.Combine(dir, "FileList.txt"), PrintStruct(Table20.FileList)); //FileCombineTable Max 0x860D0/0x64ed, count 0x73BB9
            File.WriteAllText(Path.Combine(dir, "FileOffsets.txt"), PrintStruct(Table20.FileOffsets)); //count 0x89b10
            File.WriteAllText(Path.Combine(dir, "DirectoryListLookup.txt"), PrintStruct(Table20.DirectoryListLookup)); //FolderHashToIndex Max 0x64ed, count 0x64ed
            File.WriteAllText(Path.Combine(dir, "table20_XX.txt"), PrintStruct(Table20.FieldXX)); // count 0x3ff
            File.WriteAllText(Path.Combine(dir, "FileListLookup.txt"), PrintStruct(Table20.FileListLookup)); // FileHashToIndex count 0x73375
            File.WriteAllText(Path.Combine(dir, "table20_0CB.txt"), PrintStruct(Table20.Field0CB)); // Multiplies by 100 count 0x73BB9

            File.WriteAllText(Path.Combine(dir, "table28_DirectoryListLookup.txt"), PrintStruct(Table28.DirectoryListLookup)); // FolderIndexToHash max 0x7540, count 0x7540
            File.WriteAllText(Path.Combine(dir, "table28_DirectoryList.txt"), PrintStruct(Table28.DirectoryList)); // FolderToFileList Max 0x78fd4, count 0x7540 For forward/backward iteration
            File.WriteAllText(Path.Combine(dir, "table28_EntryListLookup.txt"), PrintStruct(Table28.EntryListLookup)); // HashToIndexTable count 0x80271
            File.WriteAllText(Path.Combine(dir, "table28_0C.txt"), PrintStruct(Table28.Field0C)); // Multiplies by 100 count 0x78fd4/0x80271
            File.WriteAllText(Path.Combine(dir, "table28_EntryList.txt"), PrintStruct(Table28.EntryList)); // IndexToHashTable count 0x78fd4/0x80271 For backward iteration
        }

        public string PrintStruct(Array obj)
        {
            FieldInfo[] fields = obj.GetType().GetElementType().GetFields();
            var table = new Table(fields.Select(x => x.Name).ToArray());
            var list = new List<string>();
            var formats = new List<string>();

            foreach (var field in fields)
            {
                // Don't know of a way to check if the type is blittable other than trying it
                try
                {
                    formats.Add("X" + Marshal.SizeOf(field.FieldType) * 2);
                }
                catch (ArgumentException)
                {
                    formats.Add("");
                }
            }

            foreach (var item in obj)
            {
                list.Clear();

                for (int i = 0; i < fields.Length; i++)
                {
                    if (fields[i].GetValue(item) is IFormattable format)
                    {
                        list.Add(format.ToString(formats[i], CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        list.Add(fields[i].GetValue(item).ToString());
                    }
                }

                table.AddRow(list.ToArray());
            }

            return table.Print();
        }

        public void RemoveBadHashes()
        {
            const bool removeBad = true;
            Console.WriteLine(nameof(Table20.DirectoryList));
            foreach (var item in Table20.DirectoryList)
            {
                CheckBadHash(item.Path, item.Parent, item.Name, null, false, removeBad);
            }

            Console.WriteLine(nameof(Table20.FileList));
            foreach (var item in Table20.FileList.ToArray())
            {
                CheckBadHash(item.Path, item.Parent, item.Name, item.Extension, true, removeBad);
            }

            Console.WriteLine(nameof(Table28.EntryList));
            foreach (var item in Table28.EntryList)
            {
                CheckBadHash(item.Path, item.Parent, item.Name, item.Extension, false, removeBad);
            }

            Console.WriteLine(nameof(Table28.DirectoryList));
            foreach (var item in Table28.DirectoryList)
            {
                CheckBadHash(item.Path, item.Parent, item.Name, null, false, removeBad);
            }
        }

        public void CheckBadHash(Hash path, Hash parent, Hash name, Hash extension, bool trailingSlash, bool removeBad)
        {
            var fullPath = path.GetText();
            var nameText = name.GetText();
            var parentText = parent.GetText();
            var extensionText = extension?.GetText();

            var pathHasText = !string.IsNullOrWhiteSpace(fullPath);
            var nameHasText = !string.IsNullOrWhiteSpace(nameText);
            var parentHasText = !string.IsNullOrWhiteSpace(parentText);
            var extensionHasText = !string.IsNullOrWhiteSpace(extensionText);

            int parentDirAdd = trailingSlash ? 1 : 0;
            string separator = trailingSlash ? "" : "/";

            if (pathHasText) CheckInvalidStrings(path);
            if (nameHasText) CheckInvalidStrings(name);
            if (parentHasText) CheckInvalidStrings(parent);
            if (extensionHasText) CheckInvalidStrings(extension);

            if (parentHasText && trailingSlash)
            {
                if (parentText[parentText.Length - 1] != '/')
                {
                    Console.WriteLine($"{parentText}");
                    if (removeBad) Hash.HashStrings.Remove(parent.GetHash());
                    if (removeBad) Hash.NewStrings.Remove(parent.GetHash());
                }
            }

            if (nameHasText && extensionHasText)
            {
                var actualExt = Path.GetExtension(nameText).TrimStart('.');
                if (actualExt != extensionText)
                {
                    Console.WriteLine($"{nameText}, {extensionText}");
                    if (removeBad) Hash.HashStrings.Remove(name.GetHash());
                    if (removeBad) Hash.NewStrings.Remove(name.GetHash());
                }
            }

            if (pathHasText && extensionHasText)
            {
                var actualExt = Path.GetExtension(fullPath).TrimStart('.');
                if (actualExt != extensionText)
                {
                    Console.WriteLine($"{fullPath}, {extensionText}");
                    if (removeBad) Hash.HashStrings.Remove(path.GetHash());
                    if (removeBad) Hash.NewStrings.Remove(path.GetHash());
                }
            }

            if (pathHasText)
            {
                var expectedParentText = fullPath.Remove(Math.Max(0, Math.Min(fullPath.LastIndexOf('/') + parentDirAdd, fullPath.Length - 1)));
                var fileText = Path.GetFileName(fullPath);

                var expectedParentHash = new Hash(expectedParentText).GetHash();
                var actualParentHash = parent.GetHash();

                var expectedFileHash = new Hash(fileText).GetHash();
                var actualFileHash = name.GetHash();

                if ((expectedParentHash != actualParentHash || expectedFileHash != actualFileHash) && parent.Len != 0 && parentText != "/")
                {
                    Console.WriteLine(fullPath);
                    if (removeBad) Hash.HashStrings.Remove(path.GetHash());
                    if (removeBad) Hash.NewStrings.Remove(path.GetHash());
                }
            }

            if (pathHasText && nameHasText && parentHasText)
            {
                var expectedFull = $"{parentText}{separator}{nameText}";
                if (expectedFull != fullPath && parentText != "/")
                {
                    Console.WriteLine($"{fullPath}, {parentText}, {nameText}");
                }
            }

            void CheckInvalidStrings(Hash hash)
            {
                string text = hash.GetText();

                if (text.Contains("./") ||
                    text.Contains("//") ||
                    text.Contains("..") ||
                    text.Contains("/.") ||
                    text.EndsWith("."))
                {
                    Console.WriteLine($"Invalid string {text}");
                    if (removeBad) Hash.HashStrings.Remove(hash.GetHash());
                    if (removeBad) Hash.NewStrings.Remove(hash.GetHash());
                }
            }
        }

        public static uint Crc32(string input)
        {
            uint crc = 0xFFFFFFFF;

            foreach (char d in input)
            {
                char c = d;
                if ((uint)(c - 0x41) < 0x1A) c += (char)0x20;
                crc = CrcTable[(byte)crc ^ c] ^ (crc >> 8);
            }

            return ~crc;
        }

        public static uint Crc32(char[] input)
        {
            uint crc = 0xFFFFFFFF;

            foreach (char d in input)
            {
                char c = d;
                if (c - 0x41 < 0x1A) c += (char)0x20;
                crc = CrcTable[(byte)crc ^ c] ^ (crc >> 8);
            }

            return ~crc;
        }

        private static readonly uint[] CrcTable =
        {
            0, 0x77073096, 0xEE0E612C, 0x990951BA,
            0x076DC419, 0x706AF48F, 0xE963A535, 0x9E6495A3,
            0x0EDB8832, 0x79DCB8A4, 0xE0D5E91E, 0x97D2D988,
            0x09B64C2B, 0x7EB17CBD, 0xE7B82D07, 0x90BF1D91,
            0x1DB71064, 0x6AB020F2, 0xF3B97148, 0x84BE41DE,
            0x1ADAD47D, 0x6DDDE4EB, 0xF4D4B551, 0x83D385C7,
            0x136C9856, 0x646BA8C0, 0xFD62F97A, 0x8A65C9EC,
            0x14015C4F, 0x63066CD9, 0xFA0F3D63, 0x8D080DF5,
            0x3B6E20C8, 0x4C69105E, 0xD56041E4, 0xA2677172,
            0x3C03E4D1, 0x4B04D447, 0xD20D85FD, 0xA50AB56B,
            0x35B5A8FA, 0x42B2986C, 0xDBBBC9D6, 0xACBCF940,
            0x32D86CE3, 0x45DF5C75, 0xDCD60DCF, 0xABD13D59,
            0x26D930AC, 0x51DE003A, 0xC8D75180, 0xBFD06116,
            0x21B4F4B5, 0x56B3C423, 0xCFBA9599, 0xB8BDA50F,
            0x2802B89E, 0x5F058808, 0xC60CD9B2, 0xB10BE924,
            0x2F6F7C87, 0x58684C11, 0xC1611DAB, 0xB6662D3D,
            0x76DC4190, 0x01DB7106, 0x98D220BC, 0xEFD5102A,
            0x71B18589, 0x06B6B51F, 0x9FBFE4A5, 0xE8B8D433,
            0x7807C9A2, 0x0F00F934, 0x9609A88E, 0xE10E9818,
            0x7F6A0DBB, 0x086D3D2D, 0x91646C97, 0xE6635C01,
            0x6B6B51F4, 0x1C6C6162, 0x856530D8, 0xF262004E,
            0x6C0695ED, 0x1B01A57B, 0x8208F4C1, 0xF50FC457,
            0x65B0D9C6, 0x12B7E950, 0x8BBEB8EA, 0xFCB9887C,
            0x62DD1DDF, 0x15DA2D49, 0x8CD37CF3, 0xFBD44C65,
            0x4DB26158, 0x3AB551CE, 0xA3BC0074, 0xD4BB30E2,
            0x4ADFA541, 0x3DD895D7, 0xA4D1C46D, 0xD3D6F4FB,
            0x4369E96A, 0x346ED9FC, 0xAD678846, 0xDA60B8D0,
            0x44042D73, 0x33031DE5, 0xAA0A4C5F, 0xDD0D7CC9,
            0x5005713C, 0x270241AA, 0xBE0B1010, 0xC90C2086,
            0x5768B525, 0x206F85B3, 0xB966D409, 0xCE61E49F,
            0x5EDEF90E, 0x29D9C998, 0xB0D09822, 0xC7D7A8B4,
            0x59B33D17, 0x2EB40D81, 0xB7BD5C3B, 0xC0BA6CAD,
            0xEDB88320, 0x9ABFB3B6, 0x03B6E20C, 0x74B1D29A,
            0xEAD54739, 0x9DD277AF, 0x04DB2615, 0x73DC1683,
            0xE3630B12, 0x94643B84, 0x0D6D6A3E, 0x7A6A5AA8,
            0xE40ECF0B, 0x9309FF9D, 0x0A00AE27, 0x7D079EB1,
            0xF00F9344, 0x8708A3D2, 0x1E01F268, 0x6906C2FE,
            0xF762575D, 0x806567CB, 0x196C3671, 0x6E6B06E7,
            0xFED41B76, 0x89D32BE0, 0x10DA7A5A, 0x67DD4ACC,
            0xF9B9DF6F, 0x8EBEEFF9, 0x17B7BE43, 0x60B08ED5,
            0xD6D6A3E8, 0xA1D1937E, 0x38D8C2C4, 0x4FDFF252,
            0xD1BB67F1, 0xA6BC5767, 0x3FB506DD, 0x48B2364B,
            0xD80D2BDA, 0xAF0A1B4C, 0x36034AF6, 0x41047A60,
            0xDF60EFC3, 0xA867DF55, 0x316E8EEF, 0x4669BE79,
            0xCB61B38C, 0xBC66831A, 0x256FD2A0, 0x5268E236,
            0xCC0C7795, 0xBB0B4703, 0x220216B9, 0x5505262F,
            0xC5BA3BBE, 0xB2BD0B28, 0x2BB45A92, 0x5CB36A04,
            0xC2D7FFA7, 0xB5D0CF31, 0x2CD99E8B, 0x5BDEAE1D,
            0x9B64C2B0, 0xEC63F226, 0x756AA39C, 0x026D930A,
            0x9C0906A9, 0xEB0E363F, 0x72076785, 0x05005713,
            0x95BF4A82, 0xE2B87A14, 0x7BB12BAE, 0x0CB61B38,
            0x92D28E9B, 0xE5D5BE0D, 0x7CDCEFB7, 0x0BDBDF21,
            0x86D3D2D4, 0xF1D4E242, 0x68DDB3F8, 0x1FDA836E,
            0x81BE16CD, 0xF6B9265B, 0x6FB077E1, 0x18B74777,
            0x88085AE6, 0xFF0F6A70, 0x66063BCA, 0x11010B5C,
            0x8F659EFF, 0xF862AE69, 0x616BFFD3, 0x166CCF45,
            0xA00AE278, 0xD70DD2EE, 0x4E048354, 0x3903B3C2,
            0xA7672661, 0xD06016F7, 0x4969474D, 0x3E6E77DB,
            0xAED16A4A, 0xD9D65ADC, 0x40DF0B66, 0x37D83BF0,
            0xA9BCAE53, 0xDEBB9EC5, 0x47B2CF7F, 0x30B5FFE9,
            0xBDBDF21C, 0xCABAC28A, 0x53B39330, 0x24B4A3A6,
            0xBAD03605, 0xCDD70693, 0x54DE5729, 0x23D967BF,
            0xB3667A2E, 0xC4614AB8, 0x5D681B02, 0x2A6F2B94,
            0xB40BBE37, 0xC30C8EA1, 0x5A05DF1B, 0x2D02EF8D
        };
    }

    public class Int24
    {
        public int Value;

        public Int24(BinaryReader reader)
        {
            var a = reader.ReadByte();
            var b = reader.ReadByte();
            var c = reader.ReadByte();

            Value = (c << 16) | (b << 8) | a;
        }

        public override string ToString()
        {
            return Value.ToString("X6");
        }
    }

    public class Hash
    {
        public static Dictionary<long, string> AllHashStrings = new Dictionary<long, string>();
        public static Dictionary<long, string> HashStrings = new Dictionary<long, string>();
        public static Dictionary<long, string> NewStrings = new Dictionary<long, string>();
        public static HashSet<long> Hashes = new HashSet<long>();

        public uint Crc;
        public byte Len;

        public Hash(BinaryReader reader)
        {
            Crc = reader.ReadUInt32();
            Len = reader.ReadByte();
        }

        public Hash(string value)
        {
            Crc = Archive.Crc32(value);
            Len = (byte)value.Length;
        }

        public override string ToString()
        {
            return GetText() ?? $"{Len:X2}-{Crc:X8}";
        }

        public string GetText()
        {
            if (HashStrings.TryGetValue(GetHash(), out var str))
            {
                return str;
            }
            return null;
        }

        public bool HasText()
        {
            return Len > 0 && HashStrings.ContainsKey(GetHash());
        }

        public long GetHash()
        {
            return (long)Len << 32 | Crc;
        }

        public static void AddPerms(int len, char start, char end)
        {
            Console.WriteLine(len);
            if (len == 0) return;

            var lenOr = (long)len << 32;
            var val = new char[len];
            for (int i = 0; i < len; i++) val[i] = start;

            while (true)
            {
                for (int i = len - 1; i >= 0; i--)
                {
                    bool carry = val[i] == end;
                    val[i]++;
                    if (!carry) break;
                    if (i == 0) return;
                    val[i] = start;
                }

                var hash = Archive.Crc32(val) | lenOr;
                if (Hashes.Contains(hash))
                {
                    AddHashIfExists(new string(val).ToLowerInvariant());
                }
            }
        }

        public static long DoHash(string str)
        {
            var hash = Archive.Crc32(str);
            return (long)str.Length << 32 | hash;
        }

        public static void LoadHashes(string filename)
        {
            string[] lines = File.ReadAllLines(filename);

            foreach (var line in lines)
            {
                AddHash(line);
            }
        }

        public static void LoadDump2(string filename)
        {
            string[] lines = File.ReadAllLines(filename);

            foreach (var line in lines)
            {
                AddHashIfExists(line);
                AddHash(line);

                Split(line, '/');
                Split(line, '_');
                Split(line, '.');
            }

            void Split(string str, char del)
            {
                string[] split = str.Split(del);
                var combined = "";
                AddHashIfExists(combined);

                foreach (var s in split)
                {
                    combined += s;
                    AddHashIfExists(s);
                    //AddHash(s);
                    AddHashIfExists(combined);
                    //AddHash(combined);
                    combined += del;
                    AddHashIfExists(combined);
                    // AddHash(combined);
                    AddHashIfExists(s);
                    //AddHash(s);
                }
            }
        }

        public static void AddHash(string value)
        {
            value = value.ToLowerInvariant();
            uint hash = Archive.Crc32(value);
            long key = (long)value.Length << 32 | hash;

            //HashStrings.Add(key, value);
            HashStrings[key] = value;
            AllHashStrings[key] = value;
        }

        public static bool AddHashIfExists(string value, bool addToAllHashList = true, IProgressReport progress = null)
        {
            value = value.ToLowerInvariant();
            uint hash = Archive.Crc32(value);
            long key = (long)value.Length << 32 | hash;

            if (addToAllHashList) AllHashStrings[key] = value;

            if (Hashes.Contains(key) && !HashStrings.ContainsKey(key) && !value.Contains('%'))
            {
                HashStrings[key] = value;
                NewStrings[key] = value;
                if (progress == null)
                {
                    Console.WriteLine($"Found new hash {value}");
                }
                else
                {
                    progress.LogMessage($"Found new hash {value}");
                }

                return true;
            }

            return false;
        }
    }
}

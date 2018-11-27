using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

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

            reader.BaseStream.Position = Header.Field20;
            Table20 = new Table20(reader);

            reader.BaseStream.Position = Header.Field28;
            Table28 = new Table28(reader);
        }

        public void FindFullHashes()
        {
            foreach (var hash in Table20.Field04A)
            {
                hash.AddFullHash();
            }

            foreach (var hash in Table20.FilePathCombine)
            {
                hash.AddFullHash();
            }

            foreach (var hash in Table28.DirectoryChildren)
            {
                hash.AddFullHash();
            }

            foreach (var hash in Table28.Field10B)
            {
                hash.AddFullHash();
            }
        }

        public void ExtractStreams(string dir)
        {
            int i = 0;

            foreach (var entry in Table20.StreamFiles)
            {
                var path = Path.Combine(dir, i.ToString("D5"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var file = new FileStream(Path.Combine(dir, i.ToString("D5")), FileMode.Create, FileAccess.ReadWrite))
                {
                    Stream.Position = entry.Offset;
                    Stream.CopyStream(file, entry.Size);
                }

                i++;
            }
        }

        public void ExtractBgm(string dir)
        {
            for (int i = 0; i < 0x618; i++)
            {
                var name = Table20.StreamIndexToHash[i].Hash.GetText().Replace("stream:/", "");
                var fileIndex = Table20.StreamIndexToFile[i].FileIndex;
                var fileInfo = Table20.StreamFiles[fileIndex];
                var path = Path.Combine(dir, name);

                Directory.CreateDirectory(Path.GetDirectoryName(path));

                using (var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
                {
                    Stream.Position = fileInfo.Offset;
                    Stream.CopyStream(file, fileInfo.Size);
                }

            }
        }

        public void Print(string dir)
        {
            Directory.CreateDirectory(dir);

            File.WriteAllText(Path.Combine(dir, "table20_34.txt"), PrintStruct(Table20.StreamRoot));
            File.WriteAllText(Path.Combine(dir, "table20_38A.txt"), PrintStruct(Table20.StreamHashToIndex)); // count 0x97f
            File.WriteAllText(Path.Combine(dir, "table20_38B.txt"), PrintStruct(Table20.StreamIndexToHash)); // max 0x14f3, count 0x97f
            File.WriteAllText(Path.Combine(dir, "table20_3C.txt"), PrintStruct(Table20.StreamIndexToFile)); // max 0xaa0, count 0x1500
            File.WriteAllText(Path.Combine(dir, "table20_40.txt"), PrintStruct(Table20.StreamFiles)); //count 0xaa0
            File.WriteAllText(Path.Combine(dir, "table20_30.txt"), PrintStruct(Table20.Field30)); // Count 0xd
            File.WriteAllText(Path.Combine(dir, "table20_04A.txt"), PrintStruct(Table20.Field04A)); //FolderPathTable Max 0x73bb1/0x64d1, count 0x64ed
            File.WriteAllText(Path.Combine(dir, "table20_20.txt"), PrintStruct(Table20.Field20)); // File offset table  Max 0c860c8/0x507/0x8f82, count 0x8f82
            File.WriteAllText(Path.Combine(dir, "table20_18.txt"), PrintStruct(Table20.Field18)); //FolderIndexToHash Max 0, count 0x64d0
            File.WriteAllText(Path.Combine(dir, "table20_0CA.txt"), PrintStruct(Table20.FilePathCombine)); //FileCombineTable Max 0x860D0/0x64ed, count 0x73BB9
            File.WriteAllText(Path.Combine(dir, "table20_24.txt"), PrintStruct(Table20.Field24)); //count 0x89b10
            File.WriteAllText(Path.Combine(dir, "table20_04B.txt"), PrintStruct(Table20.Field04B)); //FolderHashToIndex Max 0x64ed, count 0x64ed
            File.WriteAllText(Path.Combine(dir, "table20_XX.txt"), PrintStruct(Table20.FieldXX)); // count 0x3ff
            File.WriteAllText(Path.Combine(dir, "table20_14.txt"), PrintStruct(Table20.FileHashToIndex)); // FileHashToIndex count 0x73375
            File.WriteAllText(Path.Combine(dir, "table20_0CB.txt"), PrintStruct(Table20.Field0CB)); // Multiplies by 100 count 0x73BB9

            File.WriteAllText(Path.Combine(dir, "table28_08A.txt"), PrintStruct(Table28.DirectoryHashToIndex)); // FolderIndexToHash max 0x7540, count 0x7540
            File.WriteAllText(Path.Combine(dir, "table28_08B.txt"), PrintStruct(Table28.DirectoryChildren)); // FolderToFileList Max 0x78fd4, count 0x7540 For forward/backward iteration
            File.WriteAllText(Path.Combine(dir, "table28_10A.txt"), PrintStruct(Table28.Field10A)); // HashToIndexTable count 0x80271
            File.WriteAllText(Path.Combine(dir, "table28_0C.txt"), PrintStruct(Table28.Field0C)); // Multiplies by 100 count 0x78fd4/0x80271
            File.WriteAllText(Path.Combine(dir, "table28_10B.txt"), PrintStruct(Table28.Field10B)); // IndexToHashTable count 0x78fd4/0x80271 For backward iteration
        }

        public string PrintStruct(Array obj)
        {
            FieldInfo[] fields = obj.GetType().GetElementType().GetFields();
            var table = new Table(fields.Select(x => x.Name).ToArray());
            var list = new List<string>();
            var formats = new List<string>();

            foreach (var field in fields)
            {
                formats.Add("X" + Marshal.SizeOf(field.FieldType) * 2);
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

        public static uint Crc32(string input)
        {
            uint crc = 0xFFFFFFFF;

            foreach (char c in input)
            {
                crc = CrcTable[(byte)crc ^ c] ^ (crc >> 8);
            }

            return ~crc;
        }

        public static string Crc32B(string input)
        {
            uint crc = 0xFFFFFFFF;

            foreach (char d in input)
            {
                char c = d;
                if (c - 65 < 0x1A) c += (char)32;
                crc = CrcTable[(byte)crc ^ c] ^ (crc >> 8);
            }

            return BitConverter.ToUInt64(BitConverter.GetBytes((ulong)(~crc | ((long)input.Length << 32))).Reverse().ToArray(), 0).ToString("X16").Substring(0, 10);
        }

        public static uint Crc32C(string input)
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

        public static uint Crc32C(char[] input)
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

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Int24
    {
        private byte A;
        private byte B;
        private byte C;

        public override string ToString()
        {
            int val = (C << 16) | (B << 8) | A;
            return val.ToString("X6");
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Hash
    {
        public static Dictionary<long, string> HashStrings = new Dictionary<long, string>();
        public static HashSet<long> Hashes = new HashSet<long>();

        public uint Crc;
        public byte Len;

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

                var hash = Archive.Crc32C(val) | lenOr;
                if (Hashes.Contains(hash))
                {
                    AddHashIfExists(new string(val).ToLowerInvariant());
                }
            }
        }

        public static long DoHash(string str)
        {
            var hash = Archive.Crc32C(str);
            return (long)str.Length << 32 | hash;
        }

        public static void LoadHashes(string filename)
        {
            string[] lines = File.ReadAllLines(filename);

            foreach (var line in lines)
            {
                var hash = Archive.Crc32C(line);
                var key = (long)line.Length << 32 | hash;

                HashStrings[key] = line;
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
            uint hash = Archive.Crc32C(value);
            long key = (long)value.Length << 32 | hash;

            if (!HashStrings.ContainsKey(key))
            {

            }
            HashStrings[key] = value;
        }

        public static bool AddHashIfExists(string value)
        {
            uint hash = Archive.Crc32C(value);
            long key = (long)value.Length << 32 | hash;

            if (Hashes.Contains(key) && !HashStrings.ContainsKey(key) && !value.Contains('%'))
            {
                HashStrings[key] = value;
                Console.WriteLine($"Found new hash {value}");
                return true;
            }

            return false;
        }
    }
}

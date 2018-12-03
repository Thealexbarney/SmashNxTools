using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Zstandard.Net;

namespace SmashNxTools
{
    static class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: program.exe data.arc");
                return;
            }

            if (!File.Exists("hashstrings.txt"))
            {
                Console.WriteLine("Could not find hashstrings.txt in the current directory");
                return;
            }

            using (var file = new FileStream(args[0], FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                // string[] subs = File.ReadAllLines("sub.txt");

                Hash.LoadHashes("hashstrings.txt");
                var archive = new Archive(file);
                LoadDump3("hashstrings.txt");
                //LoadDump3("toimport.txt");
                //LoadDump3("new hashes.txt");

                LoadDump3("comparcstrings.txt");
               // LoadDump3("comparcstrings67.txt");
                LoadDump3("dump.txt");
               // GetDecimalFormatStrings();
                //GetStringFormatStrings(archive);
                GetStringFormatStringsFromFile(archive, "format.txt");

                //FindArcStrings(archive);
                //FindNewStrings(archive);
                RemoveBadHashes(archive);
                ExportHashes(archive);
                ExtractAll(archive);
            }
        }

        public static void LoadDump3(string filename)
        {
            string[] lines = File.ReadAllLines(filename);

            foreach (var line in lines)
            {
                Hash.AddHashIfExists(line);
                Hash.AddHash(line);
               // if (line.Contains("%")) continue;

                Split(line, '/');
               // Split(line, '_');
               // Split(line, '.');
            }

            void Split(string str, char del)
            {
                string[] split = str.Split(del);
                var combined = "";
                Hash.AddHashIfExists(combined);

                foreach (var s in split)
                {
                    combined += s;
                    Hash.AddHashIfExists(s);
                    //Hash.AddHash(s);
                    Hash.AddHashIfExists(combined);
                    //Hash.AddHash(combined);
                    combined += del;
                    Hash.AddHashIfExists(combined);
                    //Hash.AddHash(combined);
                    Hash.AddHashIfExists(s);
                    //Hash.AddHash(s);
                }
            }
        }

        public static void FindArcStrings(Archive archive)
        {
            var found = new HashSet<string>();

            using (var progress = new ProgressBar())
            {
                using (var writer =
                    new StreamWriter(new FileStream("comparcstrings67.txt", FileMode.Create, FileAccess.ReadWrite),
                        Encoding.ASCII))
                {
                    foreach (var file in archive.EnumerateFiles(progress))
                    {
                        foreach (var s in FindStrings(file, 6, 7).Where(x => !found.Contains(x)))
                        {
                            found.Add(s);
                            writer.WriteLine(s);
                        }
                    }
                }
            }
        }

        public static void RemoveBadHashes(Archive archive)
        {
            archive.RemoveBadHashes();
        }

        public static void Decompress(string filename)
        {
            using (var file = new FileStream("comp1.bin", FileMode.Open))
            {
                using (var compStream = new ZstandardStream(file, CompressionMode.Decompress, true))
                {
                    using (var fileOut = new FileStream("comp1.bin.dec", FileMode.Create))
                    {
                        compStream.CopyStream(fileOut, 0xc1f0);
                    }
                }
            }
        }

        public static void ExtractAll(Archive archive)
        {
            using (var progress = new ProgressBar())
            {
                archive.ExtractFiles("files", progress);
            }
        }

        public static void ExportHashes(Archive archive)
        {
            HashSet<long> hashes = Hash.Hashes;

            var newHashes = Hash.NewStrings.Values.OrderBy(x => x.Length).ThenBy(x => x).ToArray();

            Console.WriteLine($"Found {newHashes.Length} new strings");

            if (newHashes.Length > 0) File.WriteAllLines("new hashes.txt", newHashes);

            var totalFiles = archive.Table20.FileListLookup.Length;
            var hashedFiles = archive.Table20.FileListLookup.Count(x => Hash.HashStrings.ContainsKey(x.Hash.GetHash()));

            var totalHashes = hashes.Count;
            var crackedHashes = hashes.Count(x => Hash.HashStrings.ContainsKey(x));

            Console.WriteLine($"Full file path hashes: {hashedFiles} / {totalFiles} ({(double)hashedFiles / totalFiles:P2})");
            Console.WriteLine($"All hashes: {crackedHashes} / {totalHashes} ({(double)crackedHashes / totalHashes:P2})");

            Console.WriteLine("Enter to save");
            Console.ReadLine();

            DumpStrings("hashstrings.txt");
            DumpStringsWithHashes("strings_with_hashes.txt");
            DumpHashes("hashes.txt");
            DumpUnknownHashes("hashes_unk.txt");

            Console.WriteLine("Enter to save tables");
            Console.ReadLine();
            archive.Print("tables2");
        }

        public static void FindNewStrings(Archive archive)
        {
            //Hash.LoadDump2("dump.txt");

            archive.FindFullHashes();
            archive.FindFullHashes();
            archive.FindFullHashes();
            archive.FindFullHashes();
            //AddExtensions(archive);
            //ModStrings();
            archive.FindFullHashes();
            archive.FindFullHashes();
            archive.FindFullHashes();
            archive.FindFullHashes();

            // Hash.LoadDump2("hashstrings.txt");
            //Hash.LoadDump2("arcstrings_short.txt");
            //Hash.LoadDump2("words.txt");
        }

        public static IEnumerable<string> FindStrings(byte[] data, int minLength, int maxLength = int.MaxValue)
        {
            int curLen = 0;
            for (int i = 0; i < data.Length; i++)
            {
                var c = data[i];

                if (c == 0)
                {
                    if (curLen >= minLength && curLen <= maxLength)
                    {
                        string s = Encoding.ASCII.GetString(data, i - curLen, curLen);
                        yield return s.ToLowerInvariant();
                    }

                    curLen = 0;
                }
                else if (IsValidChar(data[i]))
                {
                    curLen++;
                }
                else
                {
                    curLen = 0;
                }
            }
        }

        public static bool IsValidChar(byte c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_' || (c >= '-' && c <= '9');
        }

        public static void ModStrings()
        {
            for (int i = 0; i < 20; i++)
            {
                Hash.AddHashIfExists($"fighter_color_{i}");
                Hash.AddHashIfExists($"fighter_{i}");
            }

            string[] strings = Hash.AllHashStrings.Values.ToArray();
            var list = new List<string>();
            using (var progress = new ProgressBar())
            {
                progress.SetTotal(strings.Length);

                for (int s = 0; s < strings.Length; s++)
                {
                    string str = strings[s];
                    list.Clear();

                    //list.Add($"se_enemy_{str}.nus3audio");
                    //list.Add($"se_enemy_{str}.tonelabel");
                    //list.Add($"se_enemy_{str}.nus3bank");
                    //list.Add($"sound/sequence/fighter/{str}.sqb");
                    //list.Add($"sound/bank/fighter_voice/vc_Kirby_copy_{str}.nus3bank");
                    //list.Add($"sound/bank/fighter_voice/vc_Kirby_copy_{str}.tonelabel");
                    //list.Add($"sound/bank/fighter_voice/vc_Kirby_copy_{str}.nus3audio");
                    //list.Add($"miihat/{str}");
                    //list.Add($"miibody/{str}");
                    //list.Add($"miivoice/{str}");
                    //list.Add($"miihat/{str}/motion/body/c00/motion_list.bin");
                    //list.Add($"sound/bank/fighter_voice/{str}.nus3bank");
                    //list.Add($"sound/bank/fighter_voice/{str}.tonelabel");
                    //list.Add($"sound/bank/fighter_voice/{str}.nus3audio");
                    list.Add($"assist/{str}/param/param.prc");
                    list.Add($"pokemon/{str}/param/param.prc");
                    list.Add($"boss/{str}/param/param.prc");
                    list.Add($"item/{str}/param/param.prc");
                    list.Add($"assist/{str}/param/duet_param.prc");
                    list.Add($"pokemon/{str}/param/duet_param.prc");
                    list.Add($"boss/{str}/param/duet_param.prc");
                    list.Add($"item/{str}/param/duet_param.prc");

                    for (int i = 0; i < 9; i++)
                    {
                        //list.Add($"sound/bank/fighter/se_{str}_c{i:d2}.nus3audio");
                        //list.Add($"sound/bank/fighter/se_{str}_c{i:d2}.nus3bank");
                        //list.Add($"sound/bank/fighter/se_{str}_c{i:d2}.tonelabel");
                        //list.Add($"sound/bank/fighter_voice/vc_{str}_c{i:d2}.nus3audio");
                        //list.Add($"sound/bank/fighter_voice/vc_{str}_c{i:d2}.nus3bank");
                        //list.Add($"sound/bank/fighter_voice/vc_{str}_c{i:d2}.tonelabel");
                        //list.Add($"sound/bank/fighter_voice/vc_{str}_cheer_c{i:d2}.nus3audio");
                        //list.Add($"sound/bank/fighter_voice/vc_{str}_cheer_c{i:d2}.nus3bank");
                        //list.Add($"sound/bank/fighter_voice/vc_{str}_cheer_c{i:d2}.tonelabel");
                        //list.Add($"camera/fighter/{str}/c{i:d2}");
                        //list.Add($"standard_route_{str}.prc");
                        list.Add($"stream:/movie/technique/{str}.webm");
                        //list.Add($"{str}/c{i:d2}");
                    }

                    //if (str.Contains("c00"))
                    //{
                    //    for (int i = 0; i < 8; i++)
                    //    {
                    //        list.Add(str.Replace("c00", $"c{i:d2}"));
                    //    }
                    //}

                    //if (str.Contains("%s"))
                    //{
                    //    foreach (var str2 in strings)
                    //    {
                    //        list.Add(str.Replace("%s", str2));
                    //    }
                    // }

                    if (str.Contains("%02d"))
                    {
                        for (int i = 0; i < 20; i++)
                        {
                            list.Add(str.Replace("%02d", $"{i:d2}"));
                        }
                    }

                    if (str.Contains("%d"))
                    {
                        for (int i = 0; i < 20; i++)
                        {
                            list.Add(str.Replace("%02d", $"{i:d}"));
                        }
                    }

                    foreach (var item in list)
                    {
                        Hash.AddHashIfExists(item, false);
                    }

                    progress.ReportAdd(1);
                }
            }
        }

        public static void GetDecimalFormatStrings()
        {
            Console.WriteLine("Running decimal format");
            string[] strings = Hash.AllHashStrings.Values.ToArray();

            foreach (var str in strings)
            {
                if (str.Contains("%02d"))
                {
                    for (int i = 0; i < 20; i++)
                    {
                        Hash.AddHashIfExists(str.Replace("%02d", $"{i:d2}"));
                    }
                }

                if (str.Contains("%d"))
                {
                    for (int i = 0; i < 20; i++)
                    {
                        Hash.AddHashIfExists(str.Replace("%02d", $"{i:d}"));
                    }
                }
            }
        }

        public static void GetStringFormatStrings(Archive archive)
        {
            string[] strings = Hash.AllHashStrings.Values.ToArray();
            string[] formatStrings = strings.Where(x => x.Contains("%s")).ToArray();
            string[] extensions = archive.Table20.FileList.Select(x => $".{x.Extension.GetText()}").Where(x => x != null).Distinct().ToArray();

            using (var progress = new ProgressBar())
            {
                progress.SetTotal(formatStrings.Length);
                progress.LogMessage("Running string format");

                for (int i = 0; i < formatStrings.Length; i++)
                {
                    var str = formatStrings[i];

                    foreach (var substr in strings)
                    {
                        var replacedStr = str.Replace("%s", substr);
                        Hash.AddHashIfExists(replacedStr, false, progress);

                        //foreach (var ext in extensions)
                        //{
                        //    var extStr = replacedStr + ext;
                        //    Hash.AddHashIfExists(extStr, false);
                        //}
                    }

                    progress.ReportAdd(1);
                }
            }
        }

        public static void GetStringFormatStringsFromFile(Archive archive, string filename)
        {
            string[] strings = Hash.AllHashStrings.Values.ToArray();
            string[] formatStrings = File.ReadAllLines(filename);
            string[] extensions = archive.Table20.FileList.Select(x => $".{x.Extension.GetText()}").Where(x => x != null).Distinct().ToArray();

            using (var progress = new ProgressBar())
            {
                progress.SetTotal(formatStrings.Length);
                progress.LogMessage("Running string format");

                for (int i = 0; i < formatStrings.Length; i++)
                {
                    var str = formatStrings[i];

                    foreach (var substr in strings)
                    {
                        var replacedStr = str.Replace("%s", substr);
                        Hash.AddHashIfExists(replacedStr, false, progress);

                        //foreach (var ext in extensions)
                        //{
                        //    var extStr = replacedStr + ext;
                        //    Hash.AddHashIfExists(extStr, false);
                        //}
                    }

                    progress.ReportAdd(1);
                }
            }
        }

        public static void FindPrefix(string[] strings)
        {
            foreach (var s in strings)
            {
                for (char c = (char)0; c < 255; c++)
                {
                    if (Hash.AddHashIfExists(c + s))
                    {
                        ;
                    }
                }
            }
        }

        public static void AddExtensions(Archive archive)
        {
            Console.WriteLine("Doing extensions");
            string[] strings = Hash.AllHashStrings.Values.ToArray();

            string[] extensions = archive.Table20.FileList.Select(x => x.Extension.GetText()).Where(x => x != null).Distinct().ToArray();

            foreach (var str in strings)
            {
                foreach (var ext in extensions)
                {
                    Hash.AddHashIfExists($"{str}.{ext}", false);
                }
            }
            Console.WriteLine("Done with extensions");
        }

        public static void DumpStrings(string filename)
        {
            var strings = new List<string>();

            foreach (var hash in Hash.Hashes)
            {
                if (Hash.HashStrings.TryGetValue(hash, out var str))
                {
                    strings.Add(str);
                }
            }

            IOrderedEnumerable<string> sorted = strings.OrderBy(x => x.Length).ThenBy(x => x);
            File.WriteAllLines(filename, sorted);
        }

        public static void DumpStringsWithHashes(string filename)
        {
            var strings = new List<string>();

            foreach (var hash in Hash.Hashes)
            {
                if (Hash.HashStrings.TryGetValue(hash, out var str))
                {
                    strings.Add($"{hash >> 32:X2}-{(uint)hash:X8} {str}");
                }
            }

            IOrderedEnumerable<string> sorted = strings.OrderBy(x => x.Length).ThenBy(x => x);
            File.WriteAllLines(filename, sorted);
        }

        public static void DumpHashes(string filename)
        {
            using (var stream = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite))
            {
                using (var writer = new StreamWriter(stream))
                {
                    foreach (var hash in Hash.Hashes.OrderBy(x => x))
                    {
                        writer.WriteLine($"{hash >> 32:X2}-{(uint)hash:X8}");
                    }
                }
            }
        }

        public static void DumpUnknownHashes(string filename)
        {
            using (var stream = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite))
            {
                using (var writer = new StreamWriter(stream))
                {
                    foreach (var hash in Hash.Hashes.Where(x => !Hash.HashStrings.ContainsKey(x)).OrderBy(x => x))
                    {
                        writer.WriteLine($"{hash >> 32:X2}-{(uint)hash:X8}");
                    }
                }
            }
        }
    }
}

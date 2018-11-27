using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SmashNxTools
{
    class Program
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
                Console.WriteLine("Extracting BGM");
                archive.ExtractBgm("bgm");
                Console.WriteLine("Dumping tables");
                archive.Print("tables");
                return;
                archive.FindFullHashes();
                archive.FindFullHashes();
                archive.FindFullHashes();
                archive.FindFullHashes();
                //archive.Extract("banks");
                HashSet<long> hashes = Hash.Hashes;
               // Hash.LoadDump2("dump.txt");
               // Hash.LoadDump2("hashstrings.txt");
                //Hash.LoadDump2("arcstrings_short.txt");
                //Hash.LoadDump2("words.txt");
                //ModStrings();
                AddExtensions(archive);
                archive.FindFullHashes();
                archive.FindFullHashes();
                archive.FindFullHashes();
                archive.FindFullHashes();
                //FindPrefix(File.ReadAllLines("manual_dump.txt"));

                var timer = Stopwatch.StartNew();

                for (int i = 0; i < 7; i++)
                {
                    //Hash.AddPerms(i, 'a', 'z');
                    Console.WriteLine(timer.Elapsed.TotalMilliseconds);
                }
                timer.Stop();
                Console.WriteLine(timer.Elapsed.TotalMilliseconds);

                var totalFiles = archive.Table20.FileHashToIndex.Length;
                var hashedFiles = archive.Table20.FileHashToIndex.Count(x => Hash.HashStrings.ContainsKey(x.Hash.GetHash()));

                var totalHashes = hashes.Count;
                var crackedHashes = hashes.Count(x => Hash.HashStrings.ContainsKey(x));


                Console.WriteLine($"Full file path hashes: {hashedFiles} / {totalFiles} ({(double)hashedFiles / totalFiles:P2})");
                Console.WriteLine($"All hashes: {crackedHashes} / {totalHashes} ({(double)crackedHashes / totalHashes:P2})");

                Console.WriteLine("Enter to save");
                Console.ReadLine();

                DumpStrings("hashstrings.txt");
                DumpHashes("hashes.txt");
                DumpUnknownHashes("hashes_unk.txt");

                Console.WriteLine("Enter to save tables");
                Console.ReadLine();
                archive.Print("tables");
            }
        }

        public static void FindBadHashes(Archive archive, string filename)
        {

        }

        public static void ModStrings()
        {
            string[] strings = Hash.HashStrings.Values.ToArray();
            var list = new List<string>();

            foreach (var str in strings)
            {
                list.Clear();

                list.Add($"se_enemy_{str}.nus3audio");
                list.Add($"se_enemy_{str}.tonelabel");
                list.Add($"se_enemy_{str}.nus3bank");
                list.Add($"sound/sequence/fighter/{str}.sqb");
                list.Add($"sound/bank/fighter_voice/vc_Kirby_copy_{str}.nus3bank");
                list.Add($"sound/bank/fighter_voice/vc_Kirby_copy_{str}.tonelabel");
                list.Add($"sound/bank/fighter_voice/vc_Kirby_copy_{str}.nus3audio");
                list.Add($"miihat/{str}");
                list.Add($"miibody/{str}");
                list.Add($"miivoice/{str}");
                list.Add($"miihat/{str}/motion/body/c00/motion_list.bin");
                list.Add($"sound/bank/fighter_voice/{str}.nus3bank");
                list.Add($"sound/bank/fighter_voice/{str}.tonelabel");
                list.Add($"sound/bank/fighter_voice/{str}.nus3audio");

                for (int i = 0; i < 9; i++)
                {
                    list.Add($"sound/bank/fighter/se_{str}_c{i:d2}.nus3audio");
                    list.Add($"sound/bank/fighter/se_{str}_c{i:d2}.nus3bank");
                    list.Add($"sound/bank/fighter/se_{str}_c{i:d2}.tonelabel");
                    list.Add($"sound/bank/fighter_voice/vc_{str}_c{i:d2}.nus3audio");
                    list.Add($"sound/bank/fighter_voice/vc_{str}_c{i:d2}.nus3bank");
                    list.Add($"sound/bank/fighter_voice/vc_{str}_c{i:d2}.tonelabel");
                    list.Add($"sound/bank/fighter_voice/vc_{str}_cheer_c{i:d2}.nus3audio");
                    list.Add($"sound/bank/fighter_voice/vc_{str}_cheer_c{i:d2}.nus3bank");
                    list.Add($"sound/bank/fighter_voice/vc_{str}_cheer_c{i:d2}.tonelabel");
                    list.Add($"camera/fighter/{str}/c{i:d2}");
                    list.Add($"{str}/c{i:d2}");
                }

                if (str.Contains("c00"))
                {
                    for (int i = 0; i < 8; i++)
                    {
                        list.Add(str.Replace("c00", $"c{i:d2}"));
                    }
                }

                if (str.Contains("%s"))
                {
                    foreach (var str2 in strings)
                    {
                        list.Add(str.Replace("%s", str2));
                    }
                }

                if (str.Contains("%02d"))
                {
                    for (int i = 0; i < 8; i++)
                    {
                        list.Add(str.Replace("%02d", $"{i:d2}"));
                    }
                }

                foreach (var item in list)
                {
                    Hash.AddHashIfExists(item);
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
            string[] strings = Hash.HashStrings.Values.ToArray();

            string[] extensions = archive.Table20.FilePathCombine.Select(x => x.Extension.GetText()).Where(x => x != null).Distinct().ToArray();

            foreach (var str in strings)
            {
                foreach (var ext in extensions)
                {
                    Hash.AddHashIfExists($"{str}.{ext}");
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

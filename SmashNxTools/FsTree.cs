using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SmashNxTools
{
    public class FsTree
    {
        public Dictionary<long, FsNode> Nodes { get; set; }
        public List<FsNode> EntryNodes { get; } = new List<FsNode>();
        public List<FsNode> DirNodes { get; } = new List<FsNode>();

        public FsNode Root { get; set; }

        public FsTree(Archive archive)
        {
            ImportDirectories(archive.Table28.DirectoryList);
            ImportEntries(archive.Table28.EntryList);
            LinkNodes();
            PopulateStrings();
            SetDisplayNames();
        }

        public void ValidateNames()
        {
            foreach (var entry in EnumerateEntries())
            {
                if (entry.ParentText == null && entry.PathText != null)
                {
                    ;
                }
                if (!entry.Validate())
                {
                    Console.WriteLine($"Invalid entry {entry.PathText}, {entry.ParentText}, {entry.NameText}");
                }
            }
        }

        public IEnumerable<(string text, int length)> GetSearchStrings()
        {
            return EnumerateEntries().Select(x => x.GetStringForSearch()).Where(x => x.text != null).Distinct();
        }

        public IEnumerable<FsNode> EnumerateEntries()
        {
            var stack = new Stack<FsNode>();
            stack.Push(Root);

            while (stack.Count > 0)
            {
                FsNode curNode = stack.Pop();
                yield return curNode;

                if (curNode.NextSibling != null) stack.Push(curNode.NextSibling);
                if (curNode.FirstChild != null) stack.Push(curNode.FirstChild);
            }
        }

        private void ImportEntries(EntryListTab[] entries)
        {
            foreach (EntryListTab entry in entries.Where(x => x.Path.GetHash() != 0))
            {
                var type = (entry.Type & 0x400000) == 0 ? EntryType.File : EntryType.Dir;

                if (type == EntryType.Dir)
                {
                    var node = Nodes[entry.Path.GetHash()];
                    if (node.NameHash != entry.Name.GetHash() || node.ParentHash != entry.Parent.GetHash())
                    {
                        Console.WriteLine("Importing hash mismatch");
                    }

                    node.NextSiblingIndex = entry.NextSiblingIndex == 0xFFFFFF ? -1 : entry.NextSiblingIndex;
                    node.Type = type;
                    EntryNodes.Add(node);
                }
                else if (type == EntryType.File)
                {
                    var node = new FsNode();
                    node.PathHash = entry.Path.GetHash();
                    node.ParentHash = entry.Parent.GetHash();
                    node.NameHash = entry.Name.GetHash();
                    node.ExtensionHash = entry.Extension.GetHash();
                    node.NextSiblingIndex = entry.NextSiblingIndex == 0xFFFFFF ? -1 : entry.NextSiblingIndex;
                    node.Type = type;

                    EntryNodes.Add(node);
                    Nodes.Add(node.PathHash, node);
                }
            }
        }

        private void ImportDirectories(DirectoryListTab2[] directories)
        {
            foreach (DirectoryListTab2 dir in directories)
            {
                var node = new FsNode();
                node.PathHash = dir.Path.GetHash();
                node.ParentHash = dir.Parent.GetHash();
                node.NameHash = dir.Name.GetHash();
                node.Type = EntryType.Dir;

                node.FirstChildIndex = dir.EntryStartIndex;
                node.ChildCount = dir.EntryCount;
                node.ChildDirectoryCount = dir.ChildDirCount;
                node.ChildFileCount = dir.ChildFileCount;

                DirNodes.Add(node);
            }

            Nodes = DirNodes.ToDictionary(x => x.PathHash, x => x);
        }

        private void LinkNodes()
        {
            foreach (var node in EntryNodes)
            {
                node.Parent = Nodes[node.ParentHash];

                if (node.NextSiblingIndex != -1)
                {
                    node.NextSibling = EntryNodes[node.NextSiblingIndex];
                }

                if (node.Type == EntryType.Dir)
                {
                    node.FirstChild = EntryNodes[node.FirstChildIndex];
                }
            }
        }

        private void PopulateStrings()
        {
            foreach (FsNode node in Nodes.Values)
            {
                Hash.HashStrings.TryGetValue(node.PathHash, out string pathText);
                Hash.HashStrings.TryGetValue(node.ParentHash, out string parentText);
                Hash.HashStrings.TryGetValue(node.NameHash, out string nameText);

                node.PathText = pathText;
                node.ParentText = parentText;
                node.NameText = nameText;

                if (node.Type == EntryType.File)
                {
                    Hash.HashStrings.TryGetValue(node.ExtensionHash, out string extensionText);

                    node.ExtensionText = extensionText;
                }

                if (node.PathText == "/")
                {
                    Root = node;

                    // root isn't contained in EntryNodes and must be handled separately
                    node.FirstChild = EntryNodes[node.FirstChildIndex];
                }
            }
        }

        private void SetDisplayNames()
        {
            foreach (FsNode node in EnumerateEntries())
            {
                if (node.NameHash == 0) node.DisplayNameText = "";

                node.DisplayNameText = node.NameText ?? Program.GetHashText(node.NameHash);

                node.DisplayPathText = node.PathText ?? $"{node.Parent.DisplayPathText}/{node.DisplayNameText}";

                if (node.NameText == null && node.Type == EntryType.File) node.DisplayPathText += $"(*.{node.ExtensionText})";
            }
        }
    }

    [DebuggerDisplay("{Type}, {NameText}, {PathText}")]
    public class FsNode
    {
        public long PathHash { get; set; }
        public string PathText { get; set; }
        public long ParentHash { get; set; }
        public string ParentText { get; set; }
        public long NameHash { get; set; }
        public string NameText { get; set; }
        public long ExtensionHash { get; set; }
        public string ExtensionText { get; set; }

        public EntryType Type { get; set; }

        public FsNode Parent { get; set; }

        public int NextSiblingIndex { get; set; }
        public FsNode NextSibling { get; set; }

        public int FirstChildIndex { get; set; }
        public FsNode FirstChild { get; set; }

        public int ChildCount { get; set; }
        public int ChildFileCount { get; set; }
        public int ChildDirectoryCount { get; set; }
        public string DisplayPathText { get; set; }
        public string DisplayNameText { get; set; }

        public bool Validate()
        {
            if (ExtensionText != null && NameText != null && !NameText.EndsWith(ExtensionText)) return false;

            if (NameText != null && PathText != null && !PathText.EndsWith(NameText)) return false;

            if (PathText != null)
            {
                FsNode parent = Parent;

                // Stop before the root node
                while (parent?.Parent != null)
                {
                    if (!PathText.StartsWith(parent.PathText))
                    {
                        return false;
                    }

                    parent = parent.Parent;
                }
            }

            return true;
        }

        public (string text, int length) GetStringForSearch()
        {
            if (Type == EntryType.File && NameText == null && ExtensionText != null)
            {
                int totalLength = (int)(NameHash >> 32);
                int length = totalLength - (ExtensionText.Length + 1);
                return ($"%s.{ExtensionText}", length);
            }
            //if (Type == EntryType.File && PathText == null && ParentText != null && ExtensionText != null)
            //{
            //    int totalLength = (int)(PathHash >> 32);
            //    int length = totalLength - (ExtensionText.Length + ParentText.Length + 1);
            //    return ($"{ParentText}/%s.{ExtensionText}", length);
            //}

            if (Type == EntryType.Dir && PathText == null && ParentText != null)
            {
                int totalLength = (int)(PathHash >> 32);
                int length = totalLength - (ParentText.Length + 1);
                return ($"{ParentText}/%s", length);
            }

            return (null, 0);
        }
    }

    public enum EntryType
    {
        File,
        Dir
    }
}

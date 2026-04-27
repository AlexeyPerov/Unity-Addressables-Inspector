using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace AddressablesInspector
{
    [System.Serializable]
    public class BuildLayoutParser
    {
        public string Name { get; private set; }

        public string unityVersion;
        public string addressablesVersion;
        public List<Group> groups = new();
        public List<Archive> builtinBundles = new();

        public BuildLayoutParser(string name)
        {
            Name = name;
        }

        [System.Serializable]
        public class Group
        {
            public string name;
            public long size;
            public List<Archive> bundles = new();
        }

        [System.Serializable]
        public class Archive
        {
            public string name;
            public long size;
            public string compression;
            public long assetBundleObjectSize;
            public List<string> bundleDependencies = new();
            public List<string> expandedBundleDependencies = new();
            public List<ExplicitAsset> explicitAssets = new();
            public List<File> files = new();
        }

        [System.Serializable]
        public class ExplicitAsset
        {
            public string name;
            public long size;
            public long sizeFromObjects;
            public long sizeFromStreamedData;
            public string address;
            public List<string> externalReferences = new();
            public List<string> internalReferences = new();
            public List<string> labels = new();
        }

        [System.Serializable]
        public class File
        {
            public string name;
            public int monoScriptCount;
            public long monoScriptSize;
            public List<CAB> cabs = new();
            public List<ExplicitAsset> assets = new();
        }

        [System.Serializable]
        public class CAB
        {
            public string name;
            public long size;
        }

        public static BuildLayoutParser Load(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var text = System.IO.File.ReadAllText(path);
            return Parse(name, text);
        }

        public static BuildLayoutParser Parse(string name, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new System.ArgumentException($"argument '{nameof(text)}' must not be empty.");

            var layout = new BuildLayoutParser(name);

            var lines = new List<string>();
            foreach (var line in text.Split(new[] { '\n' }))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
            }

            for (var n = 0; n < lines.Count; ++n)
            {
                var line = lines[n];

                if (line.StartsWith("Unity Version:", System.StringComparison.Ordinal))
                {
                    layout.unityVersion = line.Substring("Unity Version:".Length).Trim();
                    continue;
                }

                if (line.StartsWith("com.unity.addressables:", System.StringComparison.Ordinal))
                {
                    layout.addressablesVersion = line.Substring("com.unity.addressables:".Length).Trim();
                    continue;
                }

                if (line.StartsWith("BuiltIn Bundles", System.StringComparison.Ordinal))
                {
                    layout.builtinBundles.AddRange(ReadBuiltInBundles(ref n));
                    continue;
                }

                if (line.StartsWith("Group ", System.StringComparison.Ordinal))
                {
                    var group = ReadGroup(ref n);
                    layout.groups.Add(group);
                    continue;
                }
            }

            layout.groups.Sort((Group a, Group b) => a.name.CompareTo(b.name));

            return layout;

            Group ReadGroup(ref int index)
            {
                var group = new Group();
                var groupLine = lines[index];
                var groupIndent = GetIndent(groupLine);

                var groupName = groupLine;
                groupName = groupName.Substring(groupName.IndexOf("Group ") + "Group ".Length);
                groupName = RemoveAttributes(groupName).Trim();
                group.name = groupName;

                foreach (var attribute in ReadAttributes(groupLine))
                {
                    if (attribute.Key == "Total Size")
                        group.size = ParseSize(attribute.Value);
                }

                var loopguard = 0;
                index++;
                for (; index < lines.Count; ++index)
                {
                loop:
                    if (++loopguard > 30000)
                    {
                        Debug.LogError($"loopguard");
                        break;
                    }
                    if (lines.Count <= index)
                        break;

                    var l = lines[index];
                    var lineIndent = GetIndent(l);
                    if (lineIndent <= groupIndent)
                    {
                        index--;
                        return group;
                    }

                    if (l.StartsWith("\tSchemas"))
                    {
                        SkipSchemas(ref index);
                        goto loop;
                    }

                    if (l.StartsWith("\tArchive"))
                    {
                        var archive = ReadArchive(ref index);
                        group.bundles.Add(archive);
                        goto loop;
                    }
                }

                return group;
            }

            List<Archive> ReadBuiltInBundles(ref int index)
            {
                var result = new List<Archive>();

                var loopguard = 0;
                index++;
                for (; index < lines.Count; ++index)
                {
                loop:
                    if (++loopguard > 30000)
                    {
                        Debug.LogError($"loopguard");
                        break;
                    }
                    if (lines.Count <= index)
                        break;

                    var l = lines[index];
                    var lineIndent = GetIndent(l);
                    if (lineIndent <= 0)
                    {
                        index--;
                        return result;
                    }

                    if (l.StartsWith("\tArchive"))
                    {
                        var archive = ReadArchive(ref index);
                        result.Add(archive);
                        goto loop;
                    }
                }

                return result;
            }

            void SkipSchemas(ref int index)
            {
                var schemasLevel = GetIndent(lines[index]);

                for (index++; index < lines.Count; ++index)
                {
                    if (GetIndent(lines[index]) <= schemasLevel)
                        break;
                }
            }

            Archive ReadArchive(ref int index)
            {
                var archive = new Archive();

                var archiveLine = lines[index];
                var archiveIndent = GetIndent(archiveLine);

                var archiveName = archiveLine;
                archiveName = archiveName.Substring(archiveName.IndexOf("Archive") + "Archive".Length);
                archiveName = RemoveAttributes(archiveName).Trim();
                archive.name = archiveName;

                foreach (var attribute in ReadAttributes(archiveLine))
                {
                    if (attribute.Key == "Size")
                        archive.size = ParseSize(attribute.Value);

                    if (attribute.Key == "Compression")
                        archive.compression = attribute.Value;

                    if (attribute.Key == "Asset Bundle Object Size")
                        archive.assetBundleObjectSize = ParseSize(attribute.Value);
                }

                var loopguard = 0;

                for (index++; index < lines.Count - 1; ++index)
                {
                    if (++loopguard > 30000)
                    {
                        Debug.LogError($"loopguard");
                        break;
                    }

                    if (GetIndent(lines[index]) <= archiveIndent)
                        break;

                    var trimmedLine = lines[index].Trim();
                    if (trimmedLine.StartsWith("Bundle Dependencies:", System.StringComparison.OrdinalIgnoreCase))
                    {
                        archive.bundleDependencies.AddRange(ReadCommaSeparatedStrings(ref index));
                        continue;
                    }

                    if (trimmedLine.StartsWith("Expanded Bundle Dependencies:", System.StringComparison.OrdinalIgnoreCase))
                    {
                        archive.expandedBundleDependencies.AddRange(ReadCommaSeparatedStrings(ref index));
                        continue;
                    }

                    if (trimmedLine.StartsWith("Explicit Assets", System.StringComparison.OrdinalIgnoreCase))
                    {
                        archive.explicitAssets.AddRange(ReadExplicitAssets(ref index));
                        continue;
                    }

                    if (trimmedLine.StartsWith("Files:", System.StringComparison.OrdinalIgnoreCase))
                    {
                        archive.files.AddRange(ReadFiles(ref index));
                        continue;
                    }
                }

                return archive;
            }

            List<File> ReadFiles(ref int index)
            {
                var files = new List<File>();
                var filesIndent = GetIndent(lines[index]);
                index++;

                for (; index < lines.Count - 1; ++index)
                {
                    var l = lines[index];
                    var lineIndent = GetIndent(l);
                    if (lineIndent <= filesIndent)
                    {
                        index--;
                        break;
                    }

                    var file = ReadFile(ref index);
                    if (file != null)
                        files.Add(file);
                }

                return files;
            }

            File ReadFile(ref int index)
            {
                var file = new File();
                var fileLine = lines[index];
                var fileIndent = GetIndent(fileLine);
                index++;

                file.name = RemoveAttributes(fileLine).Trim();
                foreach (var attribute in ReadAttributes(fileLine))
                {
                    if (attribute.Key == "MonoScripts")
                        int.TryParse(attribute.Value, out file.monoScriptCount);

                    if (attribute.Key == "MonoScript Size")
                        file.monoScriptSize = ParseSize(attribute.Value);
                }

                for (; index < lines.Count - 1; ++index)
                {
                    var l = lines[index];
                    var lineIndent = GetIndent(l);
                    if (lineIndent <= fileIndent)
                    {
                        index--;
                        break;
                    }

                    var trimmedLine = l.Trim();
                    if (trimmedLine.StartsWith("CAB-"))
                    {
                        var cab = new CAB();
                        cab.name = RemoveAttributes(trimmedLine).Trim();
                        foreach (var attribute in ReadAttributes(trimmedLine))
                        {
                            if (attribute.Key == "Size")
                                cab.size = ParseSize(attribute.Value);
                        }
                        file.cabs.Add(cab);
                        continue;
                    }

                    if (trimmedLine.StartsWith("Data From Other Assets"))
                    {
                        for (index++; index < lines.Count - 1; ++index)
                        {
                            if (GetIndent(lines[index]) <= lineIndent)
                            {
                                index--;
                                break;
                            }

                            var asset = ReadExplicitAsset(ref index);
                            if (asset != null)
                                file.assets.Add(asset);
                        }
                    }
                }

                return file;
            }

            List<ExplicitAsset> ReadExplicitAssets(ref int index)
            {
                var assets = new List<ExplicitAsset>();
                var explicitAssetsIndent = GetIndent(lines[index]);
                index++;

                for (; index < lines.Count - 1; ++index)
                {
                    var l = lines[index];
                    var lineIndent = GetIndent(l);
                    if (lineIndent <= explicitAssetsIndent)
                    {
                        index--;
                        break;
                    }

                    var asset = ReadExplicitAsset(ref index);
                    if (asset != null)
                        assets.Add(asset);
                }

                return assets;
            }

            string RemoveAttributes(string line)
            {
                var last = line.LastIndexOf(')');
                var first = last;

                var count = 1;
                for (var n = last - 1; n >= 0; --n)
                {
                    if (line[n] == ')')
                        count++;
                    if (line[n] == '(')
                        count--;
                    if (count == 0)
                    {
                        first = n;
                        break;
                    }
                }

                if (first != last && first > 0)
                {
                    line = line.Substring(0, first);
                }

                return line;
            }

            Dictionary<string, string> ReadAttributes(string line)
            {
                var last = line.LastIndexOf(')');
                var first = last;
                var result = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

                var count = 1;
                for (var n = last - 1; n >= 0; --n)
                {
                    if (line[n] == ')')
                        count++;
                    if (line[n] == '(')
                        count--;
                    if (count == 0)
                    {
                        first = n + 1;
                        break;
                    }
                }

                if (first != last)
                {
                    line = line.Substring(first, last - first);

                    foreach (var entry in line.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (string.IsNullOrEmpty(entry.Trim()))
                            continue;

                        var pair = entry.Split(new[] { ':' }, System.StringSplitOptions.RemoveEmptyEntries);
                        if (pair.Length == 2)
                            result[pair[0].Trim()] = pair[1].Trim();
                        else if (pair.Length >= 1)
                            result[pair[0].Trim()] = pair[0].Trim();
                    }
                }

                return result;
            }

            ExplicitAsset ReadExplicitAsset(ref int index)
            {
                var result = new ExplicitAsset();
                var assetLine = lines[index++];
                var assetIndent = GetIndent(assetLine);

                result.name = RemoveAttributes(assetLine).Trim();

                foreach (var attribute in ReadAttributes(assetLine))
                {
                    if (attribute.Key == "Total Size" || attribute.Key == "Size")
                        result.size = ParseSize(attribute.Value);

                    if (attribute.Key == "Addressable Name")
                        result.address = attribute.Value;

                    if (attribute.Key == "Size from Objects")
                        result.sizeFromObjects = ParseSize(attribute.Value);

                    if (attribute.Key == "Size from Streamed Data")
                        result.sizeFromStreamedData = ParseSize(attribute.Value);

                    if (attribute.Key == "Labels")
                    {
                        if (!string.IsNullOrEmpty(attribute.Value))
                        {
                            foreach (var lbl in attribute.Value.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries))
                            {
                                var trimmed = lbl.Trim();
                                if (!string.IsNullOrEmpty(trimmed))
                                    result.labels.Add(trimmed);
                            }
                        }
                    }
                }

                for (; index < lines.Count - 1; ++index)
                {
                    var l = lines[index];
                    var lineIndent = GetIndent(l);
                    if (lineIndent <= assetIndent)
                    {
                        index--;
                        return result;
                    }

                    var trimmedLine = l.Trim();
                    if (trimmedLine.StartsWith("External References:", System.StringComparison.OrdinalIgnoreCase))
                        result.externalReferences.AddRange(ReadCommaSeparatedStrings(ref index));

                    if (trimmedLine.StartsWith("Internal References:", System.StringComparison.OrdinalIgnoreCase))
                        result.internalReferences.AddRange(ReadCommaSeparatedStrings(ref index));
                }

                index--;
                return result;
            }

            List<string> ReadCommaSeparatedStrings(ref int index)
            {
                var bundlesLine = lines[index];
                if (bundlesLine.IndexOf(":") == -1)
                    return new List<string>();

                bundlesLine = bundlesLine.Substring(bundlesLine.IndexOf(":") + ":".Length);
                bundlesLine = bundlesLine.Trim();

                var bundles = new List<string>();
                foreach (var b in bundlesLine.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries))
                    bundles.Add(b.Trim());

                bundles.Sort();
                return bundles;
            }

            long ParseSize(string size)
            {
                if (size.EndsWith("GB", System.StringComparison.OrdinalIgnoreCase))
                {
                    var s = size.Substring(0, size.Length - 2);
                    return (long)(float.Parse(s, CultureInfo.InvariantCulture) * 1024 * 1024 * 1024);
                }

                if (size.EndsWith("MB", System.StringComparison.OrdinalIgnoreCase))
                {
                    var s = size.Substring(0, size.Length - 2);
                    return (long)(float.Parse(s, CultureInfo.InvariantCulture) * 1024 * 1024);
                }

                if (size.EndsWith("KB", System.StringComparison.OrdinalIgnoreCase))
                {
                    var s = size.Substring(0, size.Length - 2);
                    return (long)(float.Parse(s, CultureInfo.InvariantCulture) * 1024);
                }

                if (size.EndsWith("B", System.StringComparison.OrdinalIgnoreCase))
                {
                    var s = size.Substring(0, size.Length - 1);
                    return long.Parse(s, CultureInfo.InvariantCulture);
                }

                return -1;
            }

            int GetIndent(string s)
            {
                int count = 0;
                for (var n = 0; n < s.Length; ++n)
                {
                    if (s[n] == '\t')
                        count++;
                    else
                        break;
                }
                return count;
            }
        }
    }
}

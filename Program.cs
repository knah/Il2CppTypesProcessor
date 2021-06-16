using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Il2CppTypesProcessor
{
    internal static class Program
    {
        private static readonly string[] StructNames = {
            "MethodInfo",
            "Il2CppClass",
            "Il2CppType",
            "ParameterInfo",
            "Il2CppImage",
            "Il2CppAssembly",
            "FieldInfo",
            "EventInfo",
            "PropertyInfo",
            "VirtualInvokeData",
            "Il2CppAssemblyName"
        };

        private static int Main(string[] args)
        {
            if (args.Length <= 0)
            {
                Console.WriteLine("Usage: Il2CppTypesProcessor.exe <path to IlCppVersions/headers>");
                return 1;
            }

            var path = args[0];
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Directory at '{path}' does not exist");
                return 1;
            }
            
            var allFiles = Directory.EnumerateFiles(path, "*.h", SearchOption.TopDirectoryOnly);
            (VersionInfo VersionInfo, string FileText)[] fileToVersions = allFiles
                .Select(it => (ParseFileVersion(it), File.ReadAllText(it))).OrderBy(it => it.Item1).ToArray();

            var dict = new Dictionary<string, List<(VersionInfo VersionInfo, string StructText)>>();
            foreach (var structName in StructNames) dict[structName] = new();
            foreach (var fileToVersion in fileToVersions)
            {
                foreach (var structName in StructNames)
                {
                    var structText = ExtractStructFromFile(fileToVersion.FileText, structName);
                    dict[structName].Add((fileToVersion.VersionInfo, structText));
                }
            }

            var outDir = Path.Combine(path, "PerVersionParsed");
            foreach (var structName in StructNames)
            {
                var structOut = Path.Combine(outDir, structName);
                Directory.CreateDirectory(structOut);
                var prevStructText = dict[structName][0].StructText;
                var firstFileVersion = dict[structName][0].VersionInfo;
                File.WriteAllText(Path.Combine(structOut, firstFileVersion + ".h"), prevStructText);
                foreach (var valueTuple in dict[structName])
                {
                    if (valueTuple.StructText == prevStructText) continue;
                    
                    prevStructText = valueTuple.StructText;
                    File.WriteAllText(Path.Combine(structOut, valueTuple.VersionInfo + ".h"), prevStructText);
                }
            }

            return 0;
        }

        private static VersionInfo ParseFileVersion(string fullPath)
        {
            var fn = Path.GetFileNameWithoutExtension(fullPath)!;
            var suffix = 'o';
            var suffixNumber = 0;
            for (var i = 0; i < fn.Length ; i++)
            {
                if (!char.IsLetter(fn[i])) continue;

                suffix = fn[i];
                suffixNumber = int.Parse(fn.Substring(i + 1));
                fn = fn.Substring(0, i);
                break;
            }
            return new VersionInfo(Version.Parse(fn), suffix, suffixNumber);
        }

        private readonly struct VersionInfo : IComparable<VersionInfo>, IComparable, IEquatable<VersionInfo>
        {
            private readonly Version myVersion;
            private readonly char mySuffix;
            private readonly int mySuffixNumber;

            public VersionInfo(Version version, char suffix, int suffixNumber)
            {
                myVersion = version;
                mySuffix = suffix;
                mySuffixNumber = suffixNumber;
            }

            public bool Equals(VersionInfo other)
            {
                return myVersion.Equals(other.myVersion) && mySuffix == other.mySuffix && mySuffixNumber == other.mySuffixNumber;
            }

            public override bool Equals(object obj)
            {
                return obj is VersionInfo other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = myVersion.GetHashCode();
                    hashCode = (hashCode * 397) ^ mySuffix.GetHashCode();
                    hashCode = (hashCode * 397) ^ mySuffixNumber;
                    return hashCode;
                }
            }

            public int CompareTo(VersionInfo other)
            {
                var versionComparison = myVersion.CompareTo(other.myVersion);
                if (versionComparison != 0) return versionComparison;
                var suffixComparison = mySuffix.CompareTo(other.mySuffix);
                if (suffixComparison != 0) return suffixComparison;
                return mySuffixNumber.CompareTo(other.mySuffixNumber);
            }

            public int CompareTo(object obj)
            {
                if (ReferenceEquals(null, obj)) return 1;
                return obj is VersionInfo other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(VersionInfo)}");
            }

            public override string ToString()
            {
                return myVersion.ToString() + mySuffix + mySuffixNumber;
            }
        }

        private static string ExtractStructFromFile(string file, string structName)
        {
            var query = $"typedef struct {structName}\n";
            var structIndex = file.IndexOf(query);
            if (structIndex == -1)
                return "";
            var endIndex = structIndex + query.Length;
            int braceCount = 0;
            var hadOpenBrace = false;
            while (braceCount > 0 || !hadOpenBrace)
            {
                if (file[endIndex] == '{')
                {
                    braceCount++;
                    hadOpenBrace = true;
                } else if (file[endIndex] == '}')
                    braceCount--;
                endIndex++;
            }

            return file.Substring(structIndex, endIndex - structIndex);
        }
    }
}
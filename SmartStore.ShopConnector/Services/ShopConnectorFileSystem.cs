using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmartStore.Core;
using SmartStore.Core.Utilities;
using SmartStore.Utilities;

namespace SmartStore.ShopConnector.Services
{
    public class ShopConnectorFileSystem
    {
        private readonly string _extension = "xml";
        private readonly string _dirName;

        public ShopConnectorFileSystem(string dirName)
        {
            _dirName = dirName;
        }

        internal static string DirRoot => FileSystemHelper.TempDirTenant("ShopConnector");

        public static string ImportLogFile(bool clear = false)
        {
            string path = Path.Combine(DirRoot, "ImportLog.txt");
            if (clear)
                FileSystemHelper.ClearFile(path);
            return path;
        }

        public string Dir => GetDirectory(_dirName);

        public static string GetDirectory(string name)
        {
            if (name.HasValue())
            {
                var dir = Path.Combine(DirRoot, name);

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                return dir;
            }
            return DirRoot;
        }

        public static long GetFileSize(string path)
        {
            try
            {
                if (path.HasValue())
                {
                    var fi = new FileInfo(path);
                    return fi.Length;
                }
            }
            catch { }

            return 0;
        }

        public string GetFilePath(string nameAppendix)
        {
            var now = DateTime.Now;

            string fileName = "{0}-{1}-{2} {3}-{4}{5}.{6}".FormatInvariant(
                now.Year,
                now.Month.ToString("D2"),
                now.Day.ToString("D2"),
                now.Hour.ToString("D2"),
                now.Minute.ToString("D2"),
                nameAppendix.HasValue() ? (" " + nameAppendix) : "",
                _extension
            ).ToValidFileName("");

            return Path.Combine(Dir, fileName);
        }

        public string GetFullFilePath(string fileName)
        {
            return Path.Combine(Dir, fileName);
        }

        public static void CleanupDirectories()
        {
            Throttle.Check("Cleanup temporary Shop-Connector files", TimeSpan.FromHours(12), () =>
            {
                try
                {
                    var olderThan = TimeSpan.FromHours(12);

                    foreach (var name in new string[] { "Export", "About" })
                    {
                        var dir = GetDirectory(name);
                        FileSystemHelper.ClearDirectory(new DirectoryInfo(dir), false, olderThan);
                    }
                }
                catch { }

                return true;
            });
        }

        public List<string> GetAllFiles()
        {
            var result = new List<string>();
            var dir = new DirectoryInfo(Dir);

            foreach (var fi in dir.GetFiles("*." + _extension))
            {
                try
                {
                    result.Add(fi.Name);
                }
                catch { }
            }
            return result;
        }

        public int CountFiles()
        {
            var dir = new DirectoryInfo(Dir);
            return dir.GetFiles("*." + _extension).Length;
        }

        public bool DeleteFile(string name)
        {
            string path = Path.Combine(Dir, name);

            return FileSystemHelper.DeleteFile(path);
        }
    }


    public class ShopConnectorImportStats
    {
        private readonly string _dirName;
        private readonly string _path;

        public ShopConnectorImportStats(string dirName)
        {
            _dirName = dirName;
            _path = Path.Combine(ShopConnectorFileSystem.DirRoot, string.Concat(dirName, "ImportStats.xml"));
        }

        public void Add(ImportStats.FileStats value, bool sync = true)
        {
            Guard.NotNull(value, nameof(value));

            if (value.Name.IsEmpty())
            {
                return;
            }

            try
            {
                var oldStats = File.Exists(_path)
                    ? XmlHelper.Deserialize<ImportStats>(File.ReadAllText(_path))
                    : new ImportStats();

                var existingStats = oldStats.Files.FirstOrDefault(x => x.Name == value.Name);
                if (existingStats != null)
                {
                    existingStats.Name = value.Name;
                    existingStats.CategoryCount = value.CategoryCount;
                    existingStats.ProductCount = value.ProductCount;
                }
                else
                {
                    oldStats.Files.Add(value);
                }

                if (sync)
                {
                    try
                    {
                        var newStats = new ImportStats();
                        var existingFiles = new ShopConnectorFileSystem(_dirName).GetAllFiles();

                        foreach (var stats in oldStats.Files)
                        {
                            if (existingFiles.Contains(stats.Name))
                            {
                                newStats.Files.Add(stats);
                            }
                        }

                        File.WriteAllText(_path, XmlHelper.Serialize(newStats));
                    }
                    catch
                    {
                        File.WriteAllText(_path, XmlHelper.Serialize(oldStats));
                    }
                }
                else
                {
                    File.WriteAllText(_path, XmlHelper.Serialize(oldStats));
                }
            }
            catch (Exception ex)
            {
                ex.Dump();
            }
        }

        public ImportStats.FileStats Get(string fileName, bool estimateIfMissing = false)
        {
            ImportStats.FileStats stats = null;

            try
            {
                if (fileName.HasValue() && File.Exists(_path))
                {
                    var deserializedStats = XmlHelper.Deserialize<ImportStats>(File.ReadAllText(_path));

                    stats = deserializedStats.Files.FirstOrDefault(x => x.Name == fileName);
                }

                if (stats == null)
                {
                    stats = new ImportStats.FileStats { Name = fileName };
                }

                if (estimateIfMissing && (stats.CategoryCount == 0 || stats.ProductCount == 0))
                {
                    var importPath = new ShopConnectorFileSystem(_dirName).GetFullFilePath(fileName);
                    if (File.Exists(importPath))
                    {
                        if (stats.CategoryCount == 0)
                        {
                            stats.CategoryCount = 1000;
                        }
                        if (stats.ProductCount == 0)
                        {
                            stats.ProductCount = (int)((double)ShopConnectorFileSystem.GetFileSize(importPath) / (double)10000);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Dump();
            }

            return stats;
        }
    }

    [Serializable]
    public class ImportStats
    {
        public ImportStats()
        {
            Files = new List<FileStats>();
        }

        public List<FileStats> Files { get; set; }

        [Serializable]
        public class FileStats
        {
            public string Name { get; set; }
            public int CategoryCount { get; set; }
            public int ProductCount { get; set; }

            public override string ToString()
            {
                return $"Name: {Name.NaIfEmpty()}, Categories: {CategoryCount}, Products: {ProductCount}";
            }
        }
    }
}
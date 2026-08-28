using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace PCHealthDashboard.Services;

public class CleanableItem
{
    public string Path { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Category { get; set; } = "";
    public bool IsSelected { get; set; } = true;

    public string SizeString => SizeBytes > 1024 * 1024 * 1024 ? 
        $"{(double)SizeBytes / (1024 * 1024 * 1024):F2} GB" : 
        $"{(double)SizeBytes / (1024 * 1024):F1} MB";
}

public class StorageCleanerService
{
    public async Task<List<CleanableItem>> ScanJunkFilesAsync(string driveLetter, Action<string> progressCallback, CancellationToken token)
    {
        var items = new List<CleanableItem>();
        
        var junkDirectories = new List<(string Path, string Category)>();
        
        // If they select C drive, scan common Windows junk locations
        if (driveLetter.StartsWith("C", StringComparison.OrdinalIgnoreCase))
        {
            junkDirectories.Add((Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), "Windows Temp"));
            junkDirectories.Add((Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download"), "Windows Update Cache"));
            junkDirectories.Add((Path.GetTempPath(), "User Temp"));
        }
        
        // Scan Recycle Bin for the specific drive
        junkDirectories.Add((Path.Combine(driveLetter, "$Recycle.Bin"), "Recycle Bin"));

        // Riot Games Logs
        if (driveLetter.StartsWith("C", StringComparison.OrdinalIgnoreCase))
        {
            junkDirectories.Add((Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Riot Games", "Riot Client", "Logs"), "Riot Client Logs"));
            junkDirectories.Add((Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Riot Games", "Install Logs"), "Riot Install Logs"));
        }
        junkDirectories.Add((Path.Combine(driveLetter, "Riot Games", "League of Legends", "Logs"), "League of Legends Logs"));
        junkDirectories.Add((Path.Combine(driveLetter, "Riot Games", "VALORANT", "live", "ShooterGame", "Saved", "Logs"), "VALORANT Logs"));

        foreach (var dir in junkDirectories)
        {
            if (token.IsCancellationRequested) break;
            
            progressCallback?.Invoke($"Đang quét: {dir.Category}");
            
            if (Directory.Exists(dir.Path))
            {
                try
                {
                    var files = Directory.GetFiles(dir.Path, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        if (token.IsCancellationRequested) break;
                        try
                        {
                            var info = new FileInfo(file);
                            items.Add(new CleanableItem
                            {
                                Path = file,
                                SizeBytes = info.Length,
                                Category = dir.Category,
                                IsSelected = true
                            });
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        return items;
    }

    public async Task<List<CleanableItem>> ScanDuplicateFilesAsync(string searchDirectory, Action<string> progressCallback, CancellationToken token)
    {
        var items = new List<CleanableItem>();
        var filesBySize = new Dictionary<long, List<string>>();

        progressCallback?.Invoke("Đang tìm kiếm file...");

        try
        {
            // Recursive scan for all files in the directory safely
            var allFiles = SafeEnumerateFiles(searchDirectory).ToList();
            
            int total = allFiles.Count;
            int current = 0;

            foreach (var file in allFiles)
            {
                if (token.IsCancellationRequested) break;
                current++;

                if (current % 1000 == 0)
                {
                    progressCallback?.Invoke($"Đang phân tích: {current}/{total} files");
                }

                try
                {
                    var info = new FileInfo(file);
                    // Only consider files larger than 1MB to save time and focus on actual space hogs
                    if (info.Length > 1024 * 1024)
                    {
                        if (!filesBySize.ContainsKey(info.Length))
                        {
                            filesBySize[info.Length] = new List<string>();
                        }
                        filesBySize[info.Length].Add(file);
                    }
                }
                catch { }
            }

            // Filter out unique sizes
            var potentialDuplicates = filesBySize.Where(kvp => kvp.Value.Count > 1).ToList();
            
            int totalPotential = potentialDuplicates.Count;
            int processed = 0;

            // Hash files to confirm duplicates
            using var md5 = MD5.Create();
            
            foreach (var group in potentialDuplicates)
            {
                if (token.IsCancellationRequested) break;
                processed++;
                progressCallback?.Invoke($"Đang so sánh dữ liệu: {processed}/{totalPotential} nhóm");

                var hashGroups = new Dictionary<string, List<string>>();

                foreach (var file in group.Value)
                {
                    try
                    {
                        using var stream = File.OpenRead(file);
                        var hashBytes = md5.ComputeHash(stream);
                        var hash = BitConverter.ToString(hashBytes);

                        if (!hashGroups.ContainsKey(hash))
                        {
                            hashGroups[hash] = new List<string>();
                        }
                        hashGroups[hash].Add(file);
                    }
                    catch { }
                }

                // Add confirmed duplicates to the list
                foreach (var hashGroup in hashGroups.Where(hg => hg.Value.Count > 1))
                {
                    // Keep the first one, mark the rest for deletion
                    bool isFirst = true;
                    foreach (var file in hashGroup.Value)
                    {
                        items.Add(new CleanableItem
                        {
                            Path = file,
                            SizeBytes = group.Key,
                            Category = "File trùng lặp",
                            IsSelected = !isFirst // Select all duplicates EXCEPT the first one
                        });
                        isFirst = false;
                    }
                }
            }
        }
        catch { }

        return items;
    }

    public async Task<(int successCount, long bytesFreed)> DeleteFilesAsync(IEnumerable<CleanableItem> files, Action<string> progressCallback)
    {
        int successCount = 0;
        long bytesFreed = 0;

        foreach (var file in files.Where(f => f.IsSelected))
        {
            progressCallback?.Invoke($"Đang xóa: {Path.GetFileName(file.Path)}");
            try
            {
                File.Delete(file.Path);
                successCount++;
                bytesFreed += file.SizeBytes;
            }
            catch { }
        }

        return (successCount, bytesFreed);
    }

    private IEnumerable<string> SafeEnumerateFiles(string path)
    {
        var forbiddenFolders = new[]
        {
            "\\Windows",
            "\\Program Files",
            "\\Program Files (x86)",
            "\\ProgramData",
            "\\AppData",
            "\\$Recycle.Bin",
            "\\System Volume Information",
            "\\.nuget",
            "\\.vs",
            "\\.vscode",
            "\\.gradle"
        };

        var safeExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".sys", ".ini", ".config", ".xml", ".json", ".dat", ".db", ".sqlite"
        };

        Queue<string> queue = new Queue<string>();
        queue.Enqueue(path);
        while (queue.Count > 0)
        {
            path = queue.Dequeue();
            
            // Skip forbidden folders
            if (forbiddenFolders.Any(f => path.Contains(f, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                foreach (string subDir in Directory.GetDirectories(path))
                {
                    queue.Enqueue(subDir);
                }
            }
            catch (Exception)
            {
                // Ignore inaccessible directories
            }
            string[]? files = null;
            try
            {
                files = Directory.GetFiles(path);
            }
            catch (Exception)
            {
                // Ignore inaccessible directories
            }
            if (files != null)
            {
                foreach (string t in files)
                {
                    // Skip critical application files
                    var ext = Path.GetExtension(t);
                    if (safeExtensions.Contains(ext))
                    {
                        continue;
                    }
                    yield return t;
                }
            }
        }
    }
}

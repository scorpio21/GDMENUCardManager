using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace GDMENUCardManager.Core
{
    public class SdHealthStatus
    {
        public string HealthStatus { get; set; } = "Unknown";
        public string OperationalStatus { get; set; } = "Unknown";
        public double WriteSpeedMBs { get; set; }
        public double ReadSpeedMBs { get; set; }
        public bool IntegrityPass { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsFakeCard => !IntegrityPass && string.IsNullOrEmpty(ErrorMessage);
    }

    public static class SdHealthManager
    {
        public static async Task<SdHealthStatus> CheckHealthAsync(string sdPath, IProgress<double> progress = null, CancellationToken ct = default)
        {
            var status = new SdHealthStatus();
            try
            {
                // 1. OS-level Health Check (Fast)
                await GetOsHealthStatus(sdPath, status);

                // 2. Benchmark & Integrity (Heavy)
                if (Directory.Exists(sdPath))
                {
                    await RunBenchmarkAndIntegrity(sdPath, status, progress, ct);
                }
                else
                {
                    status.ErrorMessage = "SD Path not found or inaccessible.";
                }
            }
            catch (OperationCanceledException)
            {
                status.ErrorMessage = "Operation cancelled by user.";
            }
            catch (Exception ex)
            {
                status.ErrorMessage = ex.Message;
            }
            return status;
        }

        private static async Task GetOsHealthStatus(string sdPath, SdHealthStatus status)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await GetWindowsHealth(sdPath, status);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                await GetMacHealth(sdPath, status);
            }
            else
            {
                status.HealthStatus = "Unsupported OS";
            }
        }

        private static async Task GetWindowsHealth(string sdPath, SdHealthStatus status)
        {
            try
            {
                var driveRoot = Path.GetPathRoot(sdPath);
                if (string.IsNullOrEmpty(driveRoot)) return;

                char driveLetter = driveRoot[0];
                // Using Get-Volume first as it's often more reliable for mapped drives
                var script = $"Get-Volume -DriveLetter '{driveLetter}' | Select-Object @{{Name='HealthStatus';Expression={{$_.HealthStatus}}}}, @{{Name='OperationalStatus';Expression={{$_.OperationalStatus}}}} | ConvertTo-Json";
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -Command \"{script}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return;
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (!string.IsNullOrEmpty(output))
                {
                    status.HealthStatus = ParseSimpleJsonValue(output, "HealthStatus");
                    status.OperationalStatus = ParseSimpleJsonValue(output, "OperationalStatus");
                }

                // If still Unknown, try Get-PhysicalDisk via Partition
                if (status.HealthStatus == "Unknown")
                {
                    script = $"Get-Partition -DriveLetter '{driveLetter}' | Get-Disk | Get-PhysicalDisk | Select-Object HealthStatus, OperationalStatus | ConvertTo-Json";
                    startInfo.Arguments = $"-NoProfile -Command \"{script}\"";
                    using var process2 = Process.Start(startInfo);
                    if (process2 != null)
                    {
                        output = await process2.StandardOutput.ReadToEndAsync();
                        await process2.WaitForExitAsync();
                        if (!string.IsNullOrEmpty(output))
                        {
                            status.HealthStatus = ParseSimpleJsonValue(output, "HealthStatus");
                            status.OperationalStatus = ParseSimpleJsonValue(output, "OperationalStatus");
                        }
                    }
                }
            }
            catch { /* Keep Unknown */ }
        }

        private static async Task GetMacHealth(string sdPath, SdHealthStatus status)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "diskutil",
                    Arguments = $"info \"{sdPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return;
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                var smartLine = lines.FirstOrDefault(l => l.Contains("SMART Status:"));
                if (smartLine != null)
                {
                    status.HealthStatus = smartLine.Split(':').Last().Trim();
                }
            }
            catch { }
        }

        private static string ParseSimpleJsonValue(string json, string key)
        {
            try
            {
                var keyPart = $"\"{key}\":";
                int keyIdx = json.IndexOf(keyPart, StringComparison.OrdinalIgnoreCase);
                if (keyIdx == -1) return "Unknown";

                int valueStart = json.IndexOf("\"", keyIdx + keyPart.Length);
                if (valueStart == -1)
                {
                    // Maybe it's a number/boolean without quotes
                    int colonIdx = json.IndexOf(":", keyIdx);
                    if (colonIdx == -1) return "Unknown";
                    int nextComma = json.IndexOf(",", colonIdx);
                    int nextBrace = json.IndexOf("}", colonIdx);
                    int end = (nextComma != -1 && nextBrace != -1) ? Math.Min(nextComma, nextBrace) : Math.Max(nextComma, nextBrace);
                    if (end == -1) return "Unknown";
                    return json.Substring(colonIdx + 1, end - colonIdx - 1).Trim().Trim('\"');
                }
                
                int valueEnd = json.IndexOf("\"", valueStart + 1);
                if (valueEnd == -1) return "Unknown";

                return json.Substring(valueStart + 1, valueEnd - valueStart - 1);
            }
            catch { return "Unknown"; }
        }

        private static async Task RunBenchmarkAndIntegrity(string sdPath, SdHealthStatus status, IProgress<double> progress, CancellationToken ct)
        {
            const int testFileSize = 128 * 1024 * 1024; // 128MB
            string testFile = Path.Combine(sdPath, ".gdm_health_test");
            
            byte[] data = new byte[testFileSize];
            Random.Shared.NextBytes(data);
            
            byte[] originalHash;
            using (var sha = SHA256.Create()) 
                originalHash = sha.ComputeHash(data);

            Stopwatch sw = new Stopwatch();

            // Write test - use WriteThrough to bypass write cache
            sw.Start();
            using (var fs = new FileStream(testFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough | FileOptions.Asynchronous))
            {
                await fs.WriteAsync(data, ct);
            }
            sw.Stop();
            status.WriteSpeedMBs = (testFileSize / 1024.0 / 1024.0) / sw.Elapsed.TotalSeconds;
            progress?.Report(0.5);

            ct.ThrowIfCancellationRequested();

            // Force a small delay to let the OS settle
            await Task.Delay(500, ct);

            // Read & Integrity test
            sw.Restart();
            byte[] readData = new byte[testFileSize];
            // We can't easily bypass read cache without P/Invoke or aligned buffers, 
            // but closing and reopening with a different mode might help on some OS.
            using (var fs = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.RandomAccess))
            {
                await fs.ReadAsync(readData, ct);
            }
            sw.Stop();
            status.ReadSpeedMBs = (testFileSize / 1024.0 / 1024.0) / sw.Elapsed.TotalSeconds;

            using (var sha = SHA256.Create())
            {
                byte[] readHash = sha.ComputeHash(readData);
                status.IntegrityPass = originalHash.SequenceEqual(readHash);
            }

            if (File.Exists(testFile)) File.Delete(testFile);
            progress?.Report(1.0);
        }
    }
}

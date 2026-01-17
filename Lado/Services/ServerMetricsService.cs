using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Lado.Services
{
    public class ServerMetrics
    {
        public double CpuUsagePercent { get; set; }
        public long MemoryUsedMB { get; set; }
        public long MemoryTotalMB { get; set; }
        public double MemoryUsagePercent { get; set; }
        public List<DiskMetrics> Disks { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string ServerName { get; set; } = Environment.MachineName;
        public string OsDescription { get; set; } = RuntimeInformation.OSDescription;
        public int ProcessorCount { get; set; } = Environment.ProcessorCount;
        public TimeSpan Uptime { get; set; }
    }

    public class DiskMetrics
    {
        public string DriveName { get; set; } = "";
        public string DriveLabel { get; set; } = "";
        public long TotalGB { get; set; }
        public long UsedGB { get; set; }
        public long FreeGB { get; set; }
        public double UsagePercent { get; set; }
        public string DriveFormat { get; set; } = "";
        public string DriveType { get; set; } = "Fixed";
        public bool IsSystemDrive { get; set; }
        public int Priority { get; set; }
    }

    public interface IServerMetricsService
    {
        Task<ServerMetrics> GetMetricsAsync();
    }

    public class ServerMetricsService : IServerMetricsService
    {
        private static DateTime _lastCpuCheck = DateTime.MinValue;
        private static double _lastCpuValue = 0;
        private static TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;
        private static DateTime _lastCheckTime = DateTime.MinValue;

        public async Task<ServerMetrics> GetMetricsAsync()
        {
            var metrics = new ServerMetrics();

            // CPU Usage
            metrics.CpuUsagePercent = await GetCpuUsageAsync();

            // Memory
            GetMemoryMetrics(metrics);

            // Disk
            GetDiskMetrics(metrics);

            // Uptime
            metrics.Uptime = GetUptime();

            return metrics;
        }

        private async Task<double> GetCpuUsageAsync()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return await GetWindowsCpuUsageAsync();
                }
                else
                {
                    return await GetLinuxCpuUsageAsync();
                }
            }
            catch
            {
                return 0;
            }
        }

        private async Task<double> GetWindowsCpuUsageAsync()
        {
            var process = Process.GetCurrentProcess();

            if (_lastCheckTime == DateTime.MinValue)
            {
                _lastTotalProcessorTime = process.TotalProcessorTime;
                _lastCheckTime = DateTime.UtcNow;
                await Task.Delay(500);
            }

            var currentTime = DateTime.UtcNow;
            var currentTotalProcessorTime = process.TotalProcessorTime;

            var cpuUsedMs = (currentTotalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;
            var totalMsPassed = (currentTime - _lastCheckTime).TotalMilliseconds;

            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed) * 100;

            _lastTotalProcessorTime = currentTotalProcessorTime;
            _lastCheckTime = currentTime;

            // Para obtener uso total del sistema, usamos una aproximacion
            // basada en todos los procesos
            try
            {
                var allProcesses = Process.GetProcesses();
                double totalCpu = 0;
                int validProcesses = 0;

                foreach (var proc in allProcesses)
                {
                    try
                    {
                        if (!proc.HasExited)
                        {
                            var startTime = proc.StartTime;
                            var totalTime = proc.TotalProcessorTime;
                            var uptime = DateTime.Now - startTime;

                            if (uptime.TotalMilliseconds > 0)
                            {
                                var usage = totalTime.TotalMilliseconds / uptime.TotalMilliseconds / Environment.ProcessorCount * 100;
                                if (usage >= 0 && usage <= 100)
                                {
                                    totalCpu += usage;
                                    validProcesses++;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignorar procesos que no se pueden acceder
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }

                // Aproximacion del uso total de CPU
                return Math.Min(100, Math.Max(0, totalCpu));
            }
            catch
            {
                return Math.Min(100, Math.Max(0, cpuUsageTotal));
            }
        }

        private async Task<double> GetLinuxCpuUsageAsync()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"top -bn1 | grep 'Cpu(s)' | awk '{print $2}'\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (double.TryParse(output.Trim().Replace(",", "."), out double cpuUsage))
                    {
                        return cpuUsage;
                    }
                }
            }
            catch
            {
                // Fallback
            }

            return 0;
        }

        private void GetMemoryMetrics(ServerMetrics metrics)
        {
            try
            {
                var gcMemory = GC.GetGCMemoryInfo();

                // Memoria del proceso actual
                var process = Process.GetCurrentProcess();
                var workingSet = process.WorkingSet64;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    GetWindowsMemory(metrics);
                }
                else
                {
                    GetLinuxMemory(metrics);
                }

                // Si no pudimos obtener memoria total, usar el maximo disponible
                if (metrics.MemoryTotalMB == 0)
                {
                    metrics.MemoryTotalMB = gcMemory.TotalAvailableMemoryBytes / 1024 / 1024;
                    metrics.MemoryUsedMB = (gcMemory.TotalAvailableMemoryBytes - gcMemory.HighMemoryLoadThresholdBytes) / 1024 / 1024;
                    if (metrics.MemoryTotalMB > 0)
                    {
                        metrics.MemoryUsagePercent = Math.Round((double)metrics.MemoryUsedMB / metrics.MemoryTotalMB * 100, 1);
                    }
                }
            }
            catch
            {
                // Fallback a memoria del proceso
                var process = Process.GetCurrentProcess();
                metrics.MemoryUsedMB = process.WorkingSet64 / 1024 / 1024;
                metrics.MemoryTotalMB = 8192; // Asumimos 8GB como fallback
                metrics.MemoryUsagePercent = Math.Round((double)metrics.MemoryUsedMB / metrics.MemoryTotalMB * 100, 1);
            }
        }

        private void GetWindowsMemory(ServerMetrics metrics)
        {
            try
            {
                var gcMemory = GC.GetGCMemoryInfo();
                metrics.MemoryTotalMB = gcMemory.TotalAvailableMemoryBytes / 1024 / 1024;

                // Obtener memoria usada usando Performance Counter si es posible
                var process = Process.GetCurrentProcess();

                // Calcular memoria usada del sistema
                long committedBytes = 0;
                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        committedBytes += proc.WorkingSet64;
                    }
                    catch
                    {
                        // Ignorar procesos sin acceso
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }

                metrics.MemoryUsedMB = committedBytes / 1024 / 1024;

                if (metrics.MemoryTotalMB > 0)
                {
                    metrics.MemoryUsagePercent = Math.Round((double)metrics.MemoryUsedMB / metrics.MemoryTotalMB * 100, 1);
                    // Limitar al 100%
                    metrics.MemoryUsagePercent = Math.Min(100, metrics.MemoryUsagePercent);
                }
            }
            catch
            {
                // Usar valores por defecto
            }
        }

        private void GetLinuxMemory(ServerMetrics metrics)
        {
            try
            {
                var memInfo = File.ReadAllLines("/proc/meminfo");
                long memTotal = 0;
                long memAvailable = 0;

                foreach (var line in memInfo)
                {
                    if (line.StartsWith("MemTotal:"))
                    {
                        memTotal = ParseMemInfo(line);
                    }
                    else if (line.StartsWith("MemAvailable:"))
                    {
                        memAvailable = ParseMemInfo(line);
                    }
                }

                metrics.MemoryTotalMB = memTotal / 1024;
                metrics.MemoryUsedMB = (memTotal - memAvailable) / 1024;

                if (metrics.MemoryTotalMB > 0)
                {
                    metrics.MemoryUsagePercent = Math.Round((double)metrics.MemoryUsedMB / metrics.MemoryTotalMB * 100, 1);
                }
            }
            catch
            {
                // Usar valores por defecto
            }
        }

        private long ParseMemInfo(string line)
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && long.TryParse(parts[1], out long value))
            {
                return value;
            }
            return 0;
        }

        private void GetDiskMetrics(ServerMetrics metrics)
        {
            try
            {
                // Obtener la unidad del sistema (donde está instalado Windows/el SO)
                var systemDrive = Path.GetPathRoot(Environment.SystemDirectory)?.ToUpperInvariant() ?? "C:\\";

                // Obtener TODAS las unidades disponibles
                var allDrives = DriveInfo.GetDrives();
                var diskList = new List<DiskMetrics>();

                foreach (var drive in allDrives)
                {
                    try
                    {
                        // Saltar unidades que no están listas o son CD-ROM
                        if (!drive.IsReady || drive.DriveType == DriveType.CDRom)
                            continue;

                        // Intentar obtener información del disco
                        long totalGB = 0;
                        long freeGB = 0;
                        long usedGB = 0;
                        string driveFormat = "Unknown";

                        try
                        {
                            totalGB = drive.TotalSize / 1024 / 1024 / 1024;
                            freeGB = drive.AvailableFreeSpace / 1024 / 1024 / 1024;
                            usedGB = totalGB - freeGB;
                            driveFormat = drive.DriveFormat;
                        }
                        catch
                        {
                            // Si no podemos leer el tamaño, saltar esta unidad
                            continue;
                        }

                        // Determinar si es el disco del sistema
                        var isSystemDrive = drive.Name.ToUpperInvariant() == systemDrive;

                        // Determinar etiqueta según tipo de unidad
                        string label;
                        try
                        {
                            label = !string.IsNullOrEmpty(drive.VolumeLabel) ? drive.VolumeLabel : "";
                        }
                        catch
                        {
                            label = "";
                        }

                        if (string.IsNullOrEmpty(label))
                        {
                            label = drive.DriveType switch
                            {
                                DriveType.Network => "Unidad de Red",
                                DriveType.Removable => "Unidad Removible",
                                DriveType.Ram => "Disco RAM",
                                _ => isSystemDrive ? "Sistema" : "Disco Local"
                            };
                        }

                        // Determinar orden de prioridad
                        int priority = drive.DriveType switch
                        {
                            DriveType.Fixed => isSystemDrive ? 0 : 1,
                            DriveType.Removable => 2,
                            DriveType.Network => 3,
                            _ => 4
                        };

                        diskList.Add(new DiskMetrics
                        {
                            DriveName = drive.Name,
                            DriveLabel = label,
                            TotalGB = totalGB,
                            FreeGB = freeGB,
                            UsedGB = usedGB,
                            UsagePercent = totalGB > 0 ? Math.Round((double)usedGB / totalGB * 100, 1) : 0,
                            DriveFormat = driveFormat,
                            DriveType = drive.DriveType.ToString(),
                            IsSystemDrive = isSystemDrive,
                            Priority = priority
                        });
                    }
                    catch
                    {
                        // Ignorar discos con errores de acceso
                    }
                }

                // Ordenar y agregar a metrics
                metrics.Disks = diskList.OrderBy(d => d.Priority).ThenBy(d => d.DriveName).ToList();
            }
            catch
            {
                // No hay discos disponibles
            }
        }

        private TimeSpan GetUptime()
        {
            try
            {
                return TimeSpan.FromMilliseconds(Environment.TickCount64);
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }
    }
}

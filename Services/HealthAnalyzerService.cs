using System;

namespace PCHealthDashboard.Services;

public class HealthAnalyzerService
{
    /// <summary>
    /// Calculates Health Score (0-100) based on system architecture principles.
    /// </summary>
    public int CalculateHealthScore(
        float ssdHealth, float ssdFreeSpaceGB, float ssdTotalSpaceGB,
        float cpuTemp, float gpuTemp,
        float ramUsedGB, float ramTotalGB,
        double packetLoss)
    {
        float score = 100f;

        // 1. Amdahl's Law (I/O bottlenecks): SSD Degradation & Free Space
        if (ssdHealth < 50) score -= (50 - ssdHealth);
        else if (ssdHealth < 80) score -= (80 - ssdHealth) * 0.5f;

        if (ssdTotalSpaceGB > 0)
        {
            float freePercent = (ssdFreeSpaceGB / ssdTotalSpaceGB) * 100f;
            if (freePercent < 10) score -= (10 - freePercent) * 2f; 
        }

        // 2. Memory Hierarchy & Thrashing
        if (ramTotalGB > 0)
        {
            float ramUsagePercent = (ramUsedGB / ramTotalGB) * 100f;
            if (ramUsagePercent > 90) score -= (ramUsagePercent - 90) * 1.5f;
        }

        // 3. Thermal Throttling
        if (cpuTemp > 85) score -= (cpuTemp - 85) * 1.5f;
        if (gpuTemp > 85) score -= (gpuTemp - 85) * 1.5f;

        // 4. Network Instability (Minor impact on generic PC health)
        if (packetLoss > 5) score -= (float)(packetLoss - 5) * 0.5f;

        return (int)Math.Clamp(Math.Round(score), 0, 100);
    }
}

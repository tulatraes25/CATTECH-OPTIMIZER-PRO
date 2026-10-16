using Cattech.Optimizer.Pro.Core.Models.Diagnostics;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>Lectura read-only de eventos System 1001/41 asociados con BugCheck.</summary>
public interface ICrashEventDiscoveryService
{
    CrashEventDiscoveryResult Discover();
}

using Cattech.Optimizer.Pro.Core.Models.Diagnostics;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>Solo metadatos: no abre, copia, borra ni analiza contenido de dumps.</summary>
public interface ICrashDumpDiscoveryService
{
    CrashDumpDiscoveryResult Discover();
}

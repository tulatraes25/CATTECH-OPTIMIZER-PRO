using Cattech.Optimizer.Pro.Core.Models.Diagnostics;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Decodifica códigos Bug Check usando un catálogo local y determinista.
/// </summary>
public interface IBugCheckDecoder
{
    BugCheckDecodeResult Decode(uint code);
}

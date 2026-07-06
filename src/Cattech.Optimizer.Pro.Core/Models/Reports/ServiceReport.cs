namespace Cattech.Optimizer.Pro.Core.Models.Reports;

/// <summary>
/// Información del cliente.
/// </summary>
public class ClientInfo
{
    /// <summary>
    /// Nombre o razón social del cliente.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Email de contacto.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Teléfono de contacto.
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Dirección.
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Notas adicionales del cliente.
    /// </summary>
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Información del equipo a diagnosticar.
/// </summary>
public class EquipmentInfo
{
    /// <summary>
    /// Marca del equipo (ej: Dell, HP, Lenovo).
    /// </summary>
    public string Brand { get; set; } = string.Empty;

    /// <summary>
    /// Modelo del equipo.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Número de serie.
    /// </summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Sistema operativo instalado.
    /// </summary>
    public string OperatingSystem { get; set; } = string.Empty;

    /// <summary>
    /// Fecha de compra (si se conoce).
    /// </summary>
    public DateTime? PurchaseDate { get; set; }

    /// <summary>
    /// Número de servicio o ticket.
    /// </summary>
    public string ServiceTicket { get; set; } = string.Empty;
}

/// <summary>
/// Información del servicio/visita técnica.
/// </summary>
public class ServiceInfo
{
    /// <summary>
    /// Fecha del servicio.
    /// </summary>
    public DateTime ServiceDate { get; set; } = DateTime.Now;

    /// <summary>
    /// Motivo de la visita.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Diagnóstico realizado por el técnico.
    /// </summary>
    public string Diagnosis { get; set; } = string.Empty;

    /// <summary>
    /// Acciones realizadas.
    /// </summary>
    public List<string> ActionsPerformed { get; set; } = new();

    /// <summary>
    /// Recomendaciones para el cliente.
    /// </summary>
    public List<string> Recommendations { get; set; } = new();

    /// <summary>
    /// Notas adicionales del técnico.
    /// </summary>
    public string TechnicianNotes { get; set; } = string.Empty;
}

/// <summary>
/// Reporte de servicio completo.
/// </summary>
public class ServiceReport
{
    /// <summary>
    /// ID único del reporte.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

    /// <summary>
    /// Información del cliente.
    /// </summary>
    public ClientInfo Client { get; set; } = new();

    /// <summary>
    /// Información del equipo.
    /// </summary>
    public EquipmentInfo Equipment { get; set; } = new();

    /// <summary>
    /// Información del servicio.
    /// </summary>
    public ServiceInfo Service { get; set; } = new();

    /// <summary>
    /// Fecha de creación del reporte.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Técnico que realizó el servicio.
    /// </summary>
    public string TechnicianName { get; set; } = string.Empty;
}

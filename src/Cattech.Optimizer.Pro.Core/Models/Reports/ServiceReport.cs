namespace Cattech.Optimizer.Pro.Core.Models.Reports;

/// <summary>
/// Información del cliente.
/// </summary>
public class ClientInfo
{
    /// <summary>
    /// Nombre del cliente.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Teléfono de contacto.
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Email de contacto.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Empresa u organización.
    /// </summary>
    public string Company { get; set; } = string.Empty;

    /// <summary>
    /// Dirección.
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Observaciones adicionales del cliente.
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
    /// Tipo de equipo: Notebook, PC de escritorio, All-in-One, Otro.
    /// </summary>
    public string EquipmentType { get; set; } = string.Empty;

    /// <summary>
    /// Motivo del servicio / por qué está en reparación.
    /// </summary>
    public string ServiceReason { get; set; } = string.Empty;

    /// <summary>
    /// Observaciones adicionales del equipo.
    /// </summary>
    public string EquipmentNotes { get; set; } = string.Empty;

    /// <summary>
    /// Sistema operativo instalado.
    /// </summary>
    public string OperatingSystem { get; set; } = string.Empty;

    /// <summary>
    /// Arquitectura del SO (x64, x86, ARM64).
    /// </summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// Procesador detectado.
    /// </summary>
    public string Processor { get; set; } = string.Empty;

    /// <summary>
    /// RAM total en GB.
    /// </summary>
    public double RamTotalGB { get; set; }

    /// <summary>
    /// Disco principal detectado.
    /// </summary>
    public string PrimaryDisk { get; set; } = string.Empty;

    /// <summary>
    /// Capacidad del disco en GB.
    /// </summary>
    public double DiskCapacityGB { get; set; }

    /// <summary>
    /// Espacio libre del disco en GB.
    /// </summary>
    public double DiskFreeGB { get; set; }

    /// <summary>
    /// Tipo aproximado de disco (HDD, SSD, NVMe).
    /// </summary>
    public string DiskType { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del equipo (hostname).
    /// </summary>
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>
    /// Usuario actual del sistema.
    /// </summary>
    public string CurrentUser { get; set; } = string.Empty;

    /// <summary>
    /// Edición de Windows (Home, Pro, Enterprise).
    /// </summary>
    public string WindowsEdition { get; set; } = string.Empty;

    /// <summary>
    /// Versión/build de Windows.
    /// </summary>
    public string WindowsVersion { get; set; } = string.Empty;

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
/// Se persiste en data/service-reports/service-report-YYYYMMDD-HHMMSS.json
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

using System;

namespace BambuDashboard;

public sealed class UnitInfo
{
    public int                      Index       { get; set; }
    public float?                   Temperature { get; set; }
    public float?                   Humidity    { get; set; }
    public required IList<TrayInfo> Trays       { get; set; }
}

using System;

namespace BambuDashboard;

public sealed class UnitInfo
{
    public int                      Index          { get; set; }
    public int                      Id             { get; set; }
    public string                   Kind           { get; set; } = UnitKind.Ams;
    public string                   Name           { get; set; } = "";
    public float?                   Temperature    { get; set; }
    public float?                   Humidity       { get; set; }
    public int?                     DryTimeMinutes { get; set; }
    public required IList<TrayInfo> Trays          { get; set; }
}

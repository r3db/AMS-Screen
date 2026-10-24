using System;

namespace BambuDashboard;

public sealed class TrayInfo
{
    public int     Index    { get; set; }
    public bool    IsActive { get; set; }
    public string? Type     { get; set; }
    public string? Color    { get; set; }
    public float?  Weight   { get; set; }

}
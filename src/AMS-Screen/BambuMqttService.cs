using System;
using System.Globalization;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace BambuDashboard;

public sealed class BambuMqttService(IHubContext<PrinterHub> hubContext, IOptions<BambuPrinterOptions> printerOptions) : BackgroundService
{
    private const int Port = 8883;
    private const int ExternalTrayId = 254;
    private const int FirstHtUnitId = 128;

    private readonly IHubContext<PrinterHub> _hubCtx = hubContext;
    private readonly BambuPrinterOptions _printerOptions = printerOptions.Value;

    private List<UnitInfo> _units = [];
    private List<UnitInfo> _externals = [];
    private int _activeTrayId = -1;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var client = new MqttClientFactory().CreateMqttClient();

        client.ApplicationMessageReceivedAsync += async e =>
        {
            try
            {
                var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                if (string.IsNullOrWhiteSpace(payload) || payload.Length <= 5)
                {
                    return;
                }

                using var doc = JsonDocument.Parse(payload);

                if (doc.RootElement.TryGetProperty("print", out var json) == false)
                {
                    return;
                }

                var snapshot = UpdateState(json);

                if (snapshot is null)
                {
                    return;
                }

                await _hubCtx.Clients.All.SendAsync("AMS-Units", snapshot, ct);
            }
            catch (Exception ex)
            {
                await _hubCtx.Clients.All.SendAsync("System-Error", ex.Message, ct);
            }
        };

        client.DisconnectedAsync += async e =>
        {
            _units         = [];
            _externals     = [];
            _activeTrayId  = -1;

            await _hubCtx.Clients.All.SendAsync("System-Disconnected", ct);
            await Task.CompletedTask;
        };

        await ConnectToPrinter(client, ct);
    }

    // Helpers!
    private async Task ConnectToPrinter(IMqttClient client, CancellationToken ct)
    {
        while (ct.IsCancellationRequested == false)
        {
            try
            {
                if (client.IsConnected == false)
                {
                    var options = new MqttClientOptionsBuilder()
                        .WithTcpServer(_printerOptions.IpAddress, Port)
                        .WithCredentials("bblp", _printerOptions.LanCode)
                        .WithClientId($"bambu_local_csharp_{Guid.NewGuid().ToString()[..6]}")
                        .WithTlsOptions(o =>
                        {
                            o.UseTls();
                            o.WithAllowUntrustedCertificates();
                            o.WithCertificateValidationHandler(_ => true);
                        })
                        .Build();

                    await client.ConnectAsync(options, ct);
                    await client.SubscribeAsync($"device/{_printerOptions.Serial}/report", cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                await _hubCtx.Clients.All.SendAsync("System-Error", ex.Message, ct);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }


    // Returns the full state to send to the UI, or null when the report had nothing we care about.
    private List<UnitInfo>? UpdateState(JsonElement print)
    {
        var changed = false;

        if (print.TryGetProperty("ams", out var amsWrapper) && amsWrapper.ValueKind == JsonValueKind.Object)
        {
            if (TryGetActiveTrayId(amsWrapper, out var activeTrayId))
            {
                _activeTrayId = activeTrayId;
                changed = true;
            }

            if (amsWrapper.TryGetProperty("ams", out var amsArray) && amsArray.ValueKind == JsonValueKind.Array)
            {
                _units = ParseUnits(amsArray);
                changed = true;
            }
        }

        // Older firmware reports the external spool as "vt_tray"; newer firmware uses the "vir_slot" array.
        if (print.TryGetProperty("vt_tray", out var externalTray) && externalTray.ValueKind == JsonValueKind.Object)
        {
            _externals = [ParseExternalSpool(externalTray, ExternalTrayId, "Ext")];
            changed = true;
        }
        else if (print.TryGetProperty("vir_slot", out var virtualSlots) && virtualSlots.ValueKind == JsonValueKind.Array && virtualSlots.GetArrayLength() > 0)
        {
            _externals = ParseVirtualSlots(virtualSlots);
            changed = true;
        }

        return changed ? BuildSnapshot() : null;
    }

    private List<UnitInfo> BuildSnapshot()
    {
        var snapshot = new List<UnitInfo>(_units);

        // The external spool holder always exists, so it is always shown (as an empty slot when nothing is loaded).
        snapshot.AddRange(_externals.Count > 0 ? _externals : [CreateEmptyExternal()]);

        for (var i = 0; i < snapshot.Count; i++)
        {
            snapshot[i].Index = i;

            foreach (var tray in snapshot[i].Trays)
            {
                tray.IsActive = IsActiveTray(snapshot[i], tray);
            }
        }

        return snapshot;
    }

    private bool IsActiveTray(UnitInfo unit, TrayInfo tray)
    {
        if (tray.IsEmpty || _activeTrayId < 0)
        {
            return false;
        }

        return unit.Kind switch
        {
            UnitKind.External => _activeTrayId == unit.Id,
            UnitKind.AmsHt    => _activeTrayId == unit.Id,
            _                 => _activeTrayId == (unit.Id * 4) + tray.Index,
        };
    }

    private static List<UnitInfo> ParseUnits(JsonElement amsArray)
    {
        var units    = new List<UnitInfo>();
        var amsCount = 0;
        var htCount  = 0;

        foreach (var unit in amsArray.EnumerateArray())
        {
            var id   = unit.GetInt32OrNull("id") ?? units.Count;
            var isHt = id >= FirstHtUnitId;
            var name = isHt ? $"HT-{(char)('A' + htCount)}" : $"AMS {amsCount + 1}";

            if (isHt) { htCount++; } else { amsCount++; }

            var dryTime = unit.GetInt32OrNull("dry_time");

            units.Add(new UnitInfo
            {
                Index          = units.Count,
                Id             = id,
                Kind           = isHt ? UnitKind.AmsHt : UnitKind.Ams,
                Name           = name,
                Temperature    = ParseFloat(unit.GetStringOrNull("temp")),
                Humidity       = ParseFloat(unit.GetStringOrNull("humidity_raw")),
                DryTimeMinutes = dryTime > 0 ? dryTime : null,
                Trays          = ParseTrays(unit),
            });
        }

        return units;
    }

    private static List<TrayInfo> ParseTrays(JsonElement unit)
    {
        var result = new List<TrayInfo>();

        if (unit.TryGetProperty("tray", out var trays) == false || trays.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var tray in trays.EnumerateArray())
        {
            // Tray ids are 0-3 for an AMS; modulo keeps the slot valid if a unit reports a larger id (e.g. AMS HT).
            var trayId = tray.GetInt32OrNull("id");
            var slot   = trayId.HasValue && trayId.Value >= 0 ? trayId.Value % 4 : result.Count;

            result.Add(ParseTray(tray, slot));
        }

        return result;
    }

    // The external spool is reported on its own, outside the AMS list. An empty holder is still a unit.
    private static UnitInfo ParseExternalSpool(JsonElement externalTray, int id, string name)
    {
        return new UnitInfo
        {
            Id    = id,
            Kind  = UnitKind.External,
            Name  = name,
            Trays = [ParseTray(externalTray, 0)],
        };
    }

    // "vir_slot" holds one entry per external spool holder (254, and 255 on dual-nozzle printers).
    private static List<UnitInfo> ParseVirtualSlots(JsonElement virtualSlots)
    {
        var result = new List<UnitInfo>();

        foreach (var slot in virtualSlots.EnumerateArray())
        {
            if (slot.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = slot.GetInt32OrNull("id") ?? (ExternalTrayId + result.Count);

            result.Add(ParseExternalSpool(slot, id, result.Count == 0 ? "Ext" : $"Ext {result.Count + 1}"));
        }

        return result;
    }

    private static UnitInfo CreateEmptyExternal() => new()
    {
        Id    = ExternalTrayId,
        Kind  = UnitKind.External,
        Name  = "Ext",
        Trays = [new TrayInfo { Index = 0, IsEmpty = true }],
    };

    private static TrayInfo ParseTray(JsonElement tray, int slotIndex)
    {
        var type          = tray.GetStringOrNull("tray_type");
        var rawColor      = tray.GetStringOrNull("tray_color");
        var remainPercent = tray.GetInt32OrNull("remain");
        var totalWeight   = ParseFloat(tray.GetStringOrNull("tray_weight"));

        // Only spools the printer can measure (Bambu RFID spools) report a spool weight. For any other spool
        // "remain" is -1 or a meaningless 0, so the amount left is unknown.
        var weightKnown   = remainPercent >= 0 && totalWeight > 0f;

        // An empty slot has no filament type and no (or an all-zero) colour.
        var isEmpty = string.IsNullOrEmpty(type) && (string.IsNullOrEmpty(rawColor) || rawColor.All(c => c == '0'));

        return new TrayInfo
        {
            Index   = slotIndex,
            IsEmpty = isEmpty,
            Type    = string.IsNullOrEmpty(type) ? null : type,
            Color   = GetTrayColorHexCode(tray),
            Weight  = weightKnown ? (totalWeight!.Value * remainPercent!.Value) / 100f : null,
        };
    }

    private static bool TryGetActiveTrayId(JsonElement json, out int trayId)
    {
        trayId = -1;

        if (json.TryGetProperty("tray_now", out var currentTray))
        {
            if (currentTray.ValueKind == JsonValueKind.Number && currentTray.TryGetInt32(out var numVal))
            {
                trayId = numVal == 255 ? -1 : numVal;
                return true;
            }

            if (currentTray.ValueKind == JsonValueKind.String && int.TryParse(currentTray.GetString(), out var parsedVal))
            {
                trayId = parsedVal == 255 ? -1 : parsedVal;
                return true;
            }
        }

        return false;
    }

    private static float? ParseFloat(string? text)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? (float?)value : null;
    }

    private static string GetTrayColorHexCode(JsonElement tray)
    {
        var defaultColor = "ffffff";
        var colorHex = defaultColor;

        if (tray.TryGetProperty("tray_color", out var tc) && tc.ValueKind == JsonValueKind.String)
        {
            colorHex = tc.GetString() ?? defaultColor;
        }
        else if (tray.TryGetProperty("cols", out var colsProp) && colsProp.ValueKind == JsonValueKind.Array && colsProp.GetArrayLength() > 0)
        {
            colorHex = colsProp[0].GetString() ?? defaultColor;
        }

        if (!string.IsNullOrEmpty(colorHex) && colorHex.Length > 6)
        {
            colorHex = colorHex[..6];
        }

        return "#" + colorHex;
    }

}
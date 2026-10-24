using System;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace BambuDashboard;

public sealed class BambuMqttService(IHubContext<PrinterHub> hubContext, IOptions<BambuPrinterOptions> printerOptions) : BackgroundService
{
    private const int Port = 8883;

    private readonly IHubContext<PrinterHub> _hubCtx = hubContext;
    private readonly BambuPrinterOptions _printerOptions = printerOptions.Value;

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

                await _hubCtx.Clients.All.SendAsync("AMS-Units", GetUnits(json));
            }
            catch (Exception ex)
            {
                await _hubCtx.Clients.All.SendAsync("System-Error", ex.Message, ct);
            }
        };

        client.DisconnectedAsync += async e =>
        {
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


    private static List<UnitInfo> GetUnits(JsonElement json)
    {
        if (json.TryGetProperty("ams", out var amsWrapper) == false || amsWrapper.TryGetProperty("ams", out var amsPayload) == false)
        {
            return [];
        }

        var trayCount = 0;
        var activeTrayIndex = GetActiveTrayIndex(json);

        return [.. amsPayload
            .EnumerateArray()
            .Select((unit, unitIndex) =>
            {
                var temperature = unit.GetStringOrNull("temp");
                var humidity    = unit.GetStringOrNull("humidity_raw");
                var trays       = GetTrays(unit, trayCount, activeTrayIndex);

                trayCount += trays.Count;

                return new UnitInfo
                {
                    Index       = unitIndex,
                    Temperature = float.TryParse(temperature, out var temp) ? temp : null,
                    Humidity    = float.TryParse(humidity, out var hum) ? hum : null,
                    Trays       = trays,
                };
            })];
    }

    private static int GetActiveTrayIndex(JsonElement json)
    {
        int result = -1;

        if (json.TryGetProperty("active_tray", out var activeTrayProp) && activeTrayProp.ValueKind == JsonValueKind.Number)
        {
            result = activeTrayProp.GetInt32();
        }

        return result;
    }

    private static List<TrayInfo> GetTrays(JsonElement json, int trayCountAccumulator, int activeTrayIndex)
    {
        if (json.TryGetProperty("tray", out var trays) == false)
        {
            return [];
        }

        return [.. trays
            .EnumerateArray()
            .Select((tray, trayIndex) =>
            {
                var type          = tray.GetStringOrNull("tray_type");
                var subBrand      = tray.GetStringOrNull("tray_sub_brands");
                var trayIdName    = tray.GetStringOrNull("tray_id_name");
                var remainPercent = tray.GetInt32OrNull("remain");
                var weight        = tray.GetStringOrNull("tray_weight");
                var color         = GetTrayColorHexCode(tray);

                var index           = trayCountAccumulator + trayIndex;
                var totalWeight     = float.TryParse(weight, out float parsedW) ? parsedW : 1000f;
                var estimatedWeight = remainPercent >= 0 ? (totalWeight * remainPercent) / 100f : 0f;

                return new TrayInfo
                {
                    Index    = trayCountAccumulator + trayIndex,
                    IsActive = index == activeTrayIndex,
                    Type     = type,
                    Color    = color,
                    Weight   = estimatedWeight,
                };
            })];
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
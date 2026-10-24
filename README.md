It gives you a dedicated Bambu Lab AMS Screen via a **Raspberry PI**.<br>
But you can run this code in a **PC** a **Mac** or **Raspberry PI**.<br><br>

**This is a very early prototype.**<br>
**Your device and the printer need to be in the same network**

**UI**<br><br>
<img width="1000" height="660" alt="image" src="https://github.com/user-attachments/assets/8912e39c-28ac-478b-8855-b2c8cc6db85a" />
<br><br>
**3D Model**<br>
https://makerworld.com/en/models/3485018<br><br>
<img width="1000" height="750" alt="image" src="https://github.com/user-attachments/assets/b88b4c5f-6120-4f9e-b9c3-02fdb0e507e6" />

**Config:**
<br>
Locate `src\AMS-Screen\appsettings.json` and replace with your printer settings.
```
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "BambuPrinter": {
    "IpAddress": "My Bambu Printer IP Address",
    "LanCode": "My Bambu Printer Lan Code",
    "Serial": "My Bambu Printer Serial"
  },
  "AllowedHosts": "*"
}
```

**To Run**<br>
Go to the `src` folder and type the following:
```
dotnet run --project AMS-Screen\AMS-Screen.csproj
```

Open the specified url, most likely `http://localhost:5151`.

**To Deploy:**<br>
To deploy into a Raspberry PI please check [DEPLOY.md](DEPLOY.md) for further instructions.

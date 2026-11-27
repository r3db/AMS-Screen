It gives you a dedicated Bambu Lab AMS Screen via a **Raspberry PI**.<br>
But you can run this code in a **PC** a **Mac** or **Raspberry PI**.<br><br>

**This is a very early prototype.**<br>
**Your device and the printer need to be in the same network**

**UI**<br><br>
<img width="1292" height="889" alt="image" src="https://github.com/user-attachments/assets/244e167d-82e7-42cf-969c-b8dbc048e33b" />
<br><br>
<img width="501" height="895" alt="image" src="https://github.com/user-attachments/assets/4552a22a-2ec5-4bfa-9c06-885c1b208fb4" />
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

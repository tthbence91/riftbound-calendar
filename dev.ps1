$root = $PSScriptRoot

# Stop any previously running instances
Get-NetTCPConnection -LocalPort 5232,5253,7219 -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }

Start-Sleep -Milliseconds 500

wt `
  new-tab --title "API" --tabColor "#1e3a5f" -d $root powershell -NoExit -Command "dotnet run --project RiftboundCalendar.Api --launch-profile http" `; `
  new-tab --title "Web" --tabColor "#1a5c2a" -d $root powershell -NoExit -Command "dotnet run --project RiftboundCalendar.Web"

Start-Sleep -Seconds 4
Start-Process "chrome.exe" "--incognito http://localhost:5253"

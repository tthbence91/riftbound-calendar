$root = $PSScriptRoot

wt `
  new-tab --title "API" --tabColor "#1e3a5f" -d $root powershell -NoExit -Command "dotnet run --project RiftboundCalendar.Api --launch-profile https" `; `
  new-tab --title "Web" --tabColor "#1a5c2a" -d $root powershell -NoExit -Command "dotnet run --project RiftboundCalendar.Web"

$packageName = 'multiclip'

#$installDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
#$app = Join-Path $installDir "multiclip.exe"

# Need to execute exe to unregister server.
#Start-ChocolateyProcessAsAdmin -Statements "-kill" -ExeToRun $app

Stop-Process -Name "multiclip"
Stop-Process -Name "multiclip.server"
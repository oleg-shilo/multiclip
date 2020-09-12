$packageName = 'multiclip'
$url = 'https://github.com/oleg-shilo/multiclip/releases/download/v1.3.0.0/multiclip.7z'

# In order to avoid multiclip app popping up message boxes need to indicate that
# we are running under choco runtime by setting `UNDER_CHOCO` environment variable 
# that is to be consumed by all child processes of choco.
# Cannot use `Install-ChocolateyEnvironmentVariable` as the variable should not be 
# persisted but only set for the choco installation process. So using .NET API

[System.Environment]::SetEnvironmentVariable('UNDER_CHOCO', 'yes')

Stop-Process -Name "multiclip" -ErrorAction SilentlyContinue
Stop-Process -Name "multiclip.server" -ErrorAction SilentlyContinue

$installDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

Write-Output "--------------" 
Write-Output "Adding Windows Defender exclusion: " 
Write-Output "    $installDir" 
Write-Output "" 
Write-Output " (see https://github.com/oleg-shilo/multiclip/blob/master/README.md#installation)"
Write-Output "--------------" 

Add-MpPreference -ExclusionPath "$installDir"

$cheksum = '063FDB94A09ADFA4523096C14BA0692DA73296D2566ED41D94836B734C927B33'
$checksumType = "sha256"

# Download and unpack a zip file
Install-ChocolateyZipPackage "$packageName" "$url" "$installDir" -checksum $checksum -checksumType $checksumType

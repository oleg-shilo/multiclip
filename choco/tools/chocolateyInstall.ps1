$packageName = 'multiclip'
$url = 'https://github.com/oleg-shilo/multiclip/releases/download/v1.4.0.0/multiclip.7z'

# In order to avoid multiclip app popping up message boxes need to indicate that
# we are running under choco runtime by setting `UNDER_CHOCO` environment variable 
# that is to be consumed by all child processes of choco.
# Cannot use `Install-ChocolateyEnvironmentVariable` as the variable should not be 
# persisted but only set for the choco installation process. So using .NET API

[System.Environment]::SetEnvironmentVariable('UNDER_CHOCO', 'yes')

Stop-Process -Name "multiclip" -ErrorAction SilentlyContinue
Stop-Process -Name "multiclip.server" -ErrorAction SilentlyContinue

$installDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

$cheksum = '6DC9F54F4DA0EB55EEA6C406A4691B01DA9EDBBE95CA8DC733F709825AC63112'
$checksumType = "sha256"

# Download and unpack a zip file
Install-ChocolateyZipPackage "$packageName" "$url" "$installDir" -checksum $checksum -checksumType $checksumType

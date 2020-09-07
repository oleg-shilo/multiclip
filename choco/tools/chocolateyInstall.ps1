$packageName = 'multiclip'
$url = 'https://github.com/oleg-shilo/multiclip/releases/download/v1.2.0.3/multiclip.7z'

# In order to avoid multiclip app poping up message boxes need to indicate that
# we are running under choco runtime by setting `UNDER_CHOCO` evironment variable 
# that is to be consumed by all child processes of choco.
# Cannot use `Install-ChocolateyEnvironmentVariable` as the variable should not be 
# persisted but only set for the choco installation process. So using .NET API

[System.Environment]::SetEnvironmentVariable('UNDER_CHOCO', 'yes')

Stop-Process -Name "multiclip"
Stop-Process -Name "multiclip.server"

$installDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

$cheksum = 'B5322E89143D2E03336851AFE27DA969A2071CE75D6777D4130CFACD16D35A67'
$checksumType = "sha256"

# Download and unpack a zip file
Install-ChocolateyZipPackage "$packageName" "$url" "$installDir" -checksum $checksum -checksumType $checksumType

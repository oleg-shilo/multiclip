$packageName = 'multiclip'
$url = 'https://github.com/oleg-shilo/multiclip/releases/download/v1.2.1.0/multiclip.7z'

try {
  [System.Environment]::SetEnvironmentVariable('UNDER_CHOCO', 'yes')
  Stop-Process -Name "multiclip"
  Stop-Process -Name "multiclip.server"

  $installDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

  $cheksum = '103828853DCFF559AD39ED190A629508E47776966A68F99200AC73FD97EAA411'
  $checksumType = "sha256"

  # Download and unpack a zip file
  Install-ChocolateyZipPackage "$packageName" "$url" "$installDir" -checksum $checksum -checksumType $checksumType

} catch {
  throw $_.Exception
}

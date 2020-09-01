$packageName = 'multiclip'
$url = 'https://github.com/oleg-shilo/multiclip/releases/download/v1.2.0.3/multiclip.7z'

try {
  [System.Environment]::SetEnvironmentVariable('UNDER_CHOCO', 'yes')

  $installDir = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"

  $cheksum = 'B5322E89143D2E03336851AFE27DA969A2071CE75D6777D4130CFACD16D35A67'
  $checksumType = "sha256"

  # Download and unpack a zip file
  Install-ChocolateyZipPackage "$packageName" "$url" "$installDir" -checksum $checksum -checksumType $checksumType

} catch {
  throw $_.Exception
}

# Build
1. 7Zip 'multiclip.exe'

# Package
1.  Update update_package.cs with URL and the latest version
2.  Run update_package.cs
3.  Update *.nuspec with the latest version number
4.  Update *.nuspec with the latest release notes
5.  Update publish.cmd with the latest version

MUST be CMD but not PS !!!!

5.  Run build.cmd or "choco pack"
 5.a If required run "choco install multiclip -y -s '%cd%'" or "choco upgrade multiclip -y -s '%cd%'"
6.  Run publish.cmd

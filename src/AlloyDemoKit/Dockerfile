# escape=`

FROM microsoft/aspnet:4.7.2-windowsservercore-1803

RUN Add-WindowsFeature Web-WebSockets

# Fix permission issues
WORKDIR /inetpub/wwwroot
RUN icacls ./* /grant everyone:F /T

# https://blog.sixeyed.com/dockerizing-nerd-dinner-part-1-running-a-legacy-asp-net-app-in-a-windows-container/
# Add SqlLocalDb
RUN Invoke-WebRequest -OutFile c:\SqlLocalDB.msi -Uri http://download.microsoft.com/download/8/D/D/8DD7BDBA-CEF7-4D8E-8C16-D9F69527F909/ENU/x64/SqlLocalDB.MSI
RUN ["cmd", "/S", "/C", "c:\\windows\\syswow64\\msiexec", "/i", "c:\\SqlLocalDB.msi", "IACCEPTSQLLOCALDBLICENSETERMS=YES", "/qn"]
RUN & 'C:\Program Files\Microsoft SQL Server\110\Tools\Binn\sqllocaldb' start "v11.0"

# Allow using localdb
RUN Import-Module WebAdministration; `  
    Set-ItemProperty 'IIS:\AppPools\DefaultAppPool' -Name 'processModel.loadUserProfile' -Value 'True'; `
    Set-ItemProperty 'IIS:\AppPools\DefaultAppPool' -Name 'processModel.setProfileEnvironment' -Value 'True'

ARG source
WORKDIR /inetpub/wwwroot
COPY ${source:-obj/Docker/publish} .
FROM microsoft/aspnet:4.7.1-windowsservercore-1709
RUN Add-WindowsFeature Web-WebSockets
ARG source
WORKDIR /inetpub/wwwroot
COPY ${source:-obj/Docker/publish} .
RUN icacls ./* /grant everyone:F /T

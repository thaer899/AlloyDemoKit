FROM microsoft/aspnet:4.6.2
RUN Add-WindowsFeature Web-WebSockets
ARG source
WORKDIR /inetpub/wwwroot
COPY ${source:-obj/Docker/publish} .

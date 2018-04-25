FROM microsoft/mssql-server-linux:latest
ENV ACCEPT_EULA Y
ENV SA_PASSWORD All0yDemokit!
# Create app directory
RUN mkdir -p /usr/src/app
WORKDIR /usr/src/app

COPY . /usr/src/app/

# Grant permissions for the import-data script to be executable
RUN chmod +x /usr/src/app/import-data.sh

CMD /bin/bash ./entrypoint.sh
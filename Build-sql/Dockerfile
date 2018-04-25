FROM microsoft/mssql-server-windows-developer:1709
COPY ./alloydemokit.mdf .
COPY ./alloydemokit_log.ldf .
ENV attach_dbs='[{"dbName":"alloydemokit","dbFiles":["C:\\alloydemokit.mdf","C:\\alloydemokit_log.ldf"]}]'
# Imagen multi-etapa del backend. Cada servicio de compose elige su etapa final con `target`.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS compilar
WORKDIR /src
COPY . .
RUN dotnet restore Inventario.slnx

# Pruebas (RD-12): se compilan en la imagen y corren sin base de datos ni API.
FROM compilar AS pruebas
RUN dotnet build Inventario.slnx -c Release --no-restore
ENTRYPOINT ["dotnet", "test", "Inventario.slnx", "-c", "Release", "--no-build"]

FROM compilar AS publicar-api
RUN dotnet publish src/Inventario.Api -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=publicar-api /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "Inventario.Api.dll"]

FROM compilar AS publicar-enviador
RUN dotnet publish src/Inventario.Enviador -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS enviador
WORKDIR /app
COPY --from=publicar-enviador /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "Inventario.Enviador.dll"]

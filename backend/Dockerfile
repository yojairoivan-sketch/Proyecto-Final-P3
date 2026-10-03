# Imagen multi-etapa del backend. Cada servicio de compose elige su etapa final con `target`.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS compilar
WORKDIR /src
COPY . .
RUN dotnet restore Inventario.slnx

FROM compilar AS publicar-api
RUN dotnet publish src/Inventario.Api -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=publicar-api /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "Inventario.Api.dll"]

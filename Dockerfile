# Receta de la imagen de la aplicación. Una sola para todo el proyecto: cada servicio de docker-compose.yml
# elige su etapa final con `target`.
#   api       la API (ASP.NET Core): reglas, base de datos y documentos
#   web       la interfaz (React) servida por nginx, que además reenvía /api a la API y /correo a Mailpit
#   enviador  el proceso aparte que entrega la cola de correos
#   pruebas   las suites automáticas (docker-compose.pruebas.yml)
# Lo que no entra a la imagen (dependencias instaladas, compilados, el historial de git, .env) está en .dockerignore.

# ---------- Backend (.NET 10) ----------

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS compilar
WORKDIR /src
COPY backend/ .
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
# Carpeta de los documentos subidos. compose monta encima el volumen «documentos» (RF-DOC-05), así que los archivos
# nunca quedan dentro de la imagen. Se crea aquí con el usuario de la aplicación como dueño para que el volumen
# nazca con ese dueño y la API pueda escribir en él.
RUN mkdir -p /datos/documentos && chown $APP_UID /datos/documentos
ENV DOCUMENTOS_CARPETA=/datos/documentos
USER $APP_UID
ENTRYPOINT ["dotnet", "Inventario.Api.dll"]

FROM compilar AS publicar-enviador
RUN dotnet publish src/Inventario.Enviador -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS enviador
WORKDIR /app
COPY --from=publicar-enviador /out .
USER $APP_UID
ENTRYPOINT ["dotnet", "Inventario.Enviador.dll"]

# ---------- Frontend (React + Vite, servido con nginx) ----------

FROM node:24-alpine AS compilar-web
WORKDIR /app
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ .
RUN npm run build

FROM nginx:1.29-alpine AS web
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=compilar-web /app/dist /usr/share/nginx/html

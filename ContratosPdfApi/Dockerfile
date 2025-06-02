FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Instalar dependencias para wkhtmltopdf
RUN apt-get update && apt-get install -y \
    wget \
    fontconfig \
    libfreetype6 \
    libjpeg62-turbo \
    libpng16-16 \
    libx11-6 \
    libxcb1 \
    libxext6 \
    libxrender1 \
    xfonts-75dpi \
    xfonts-base \
    && rm -rf /var/lib/apt/lists/*

# Descargar e instalar wkhtmltopdf
RUN wget https://github.com/wkhtmltopdf/packaging/releases/download/0.12.6.1-2/wkhtmltox_0.12.6.1-2.bullseye_amd64.deb \
    && dpkg -i wkhtmltox_0.12.6.1-2.bullseye_amd64.deb \
    && rm wkhtmltox_0.12.6.1-2.bullseye_amd64.deb

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar el archivo de proyecto
COPY ["ContratosPdfApi.csproj", "ContratosPdfApi/"]
RUN dotnet restore "ContratosPdfApi/ContratosPdfApi.csproj"

# Copiar todo el código fuente
COPY . ContratosPdfApi/
WORKDIR "/src/ContratosPdfApi"
RUN dotnet build "ContratosPdfApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ContratosPdfApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Crear directorio wwwroot
RUN mkdir -p wwwroot/assets wwwroot/temp

# Copiar wwwroot solo si existe (opcional)
COPY --from=build /src/ContratosPdfApi/wwwroot ./wwwroot/ 2>/dev/null || echo "No wwwroot directory found, skipping..."

ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "ContratosPdfApi.dll"]
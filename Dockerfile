FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Instalar dependencias y wkhtmltopdf desde repositorios oficiales
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
    wkhtmltopdf \
    && rm -rf /var/lib/apt/lists/*

# Verificar que wkhtmltopdf esté instalado
RUN wkhtmltopdf --version

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar el archivo de proyecto
COPY ["ContratosPdfApi/ContratosPdfApi.csproj", "ContratosPdfApi/"]
RUN dotnet restore "ContratosPdfApi/ContratosPdfApi.csproj"

# Copiar todo el código fuente
COPY ContratosPdfApi/ ContratosPdfApi/
WORKDIR "/src/ContratosPdfApi"
RUN dotnet build "ContratosPdfApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ContratosPdfApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Copiar wwwroot desde el build stage (incluye assets)
COPY --from=build /src/ContratosPdfApi/wwwroot ./wwwroot/

# Crear directorios adicionales si no existen
RUN mkdir -p wwwroot/temp

ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "ContratosPdfApi.dll"]
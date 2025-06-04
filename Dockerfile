FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Instalar dependencias necesarias y wkhtmltopdf con librerías
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
    libssl3 \
    libgcc-s1 \
    xfonts-75dpi \
    xfonts-base \
    && rm -rf /var/lib/apt/lists/*

# Descargar e instalar wkhtmltopdf con todas las librerías necesarias
RUN wget https://github.com/wkhtmltopdf/packaging/releases/download/0.12.6.1-3/wkhtmltox_0.12.6.1-3.bookworm_amd64.deb \
    && apt-get update \
    && apt-get install -y ./wkhtmltox_0.12.6.1-3.bookworm_amd64.deb \
    && rm wkhtmltox_0.12.6.1-3.bookworm_amd64.deb \
    && rm -rf /var/lib/apt/lists/*

# Verificar instalación y crear enlaces simbólicos necesarios
RUN wkhtmltopdf --version \
    && ldconfig \
    && find /usr -name "*wkhtmltox*" -type f 2>/dev/null || echo "Archivos wkhtmltox encontrados" \
    && ls -la /usr/local/lib/ || echo "Contenido de /usr/local/lib/"

# Crear enlaces simbólicos para que DinkToPdf encuentre las librerías
RUN if [ -f /usr/local/lib/libwkhtmltox.so.0 ]; then \
    ln -sf /usr/local/lib/libwkhtmltox.so.0 /usr/local/lib/libwkhtmltox.so; \
    fi \
    && if [ -f /usr/local/lib/libwkhtmltox.so ]; then \
    ln -sf /usr/local/lib/libwkhtmltox.so /app/libwkhtmltox.so; \
    fi

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

# Crear directorios necesarios
RUN mkdir -p wwwroot/Uploads/Contratos/Bienes/PDFs \
    && mkdir -p wwwroot/Uploads/Contratos/Bienes/TablaCantidades \
    && mkdir -p wwwroot/Uploads/Contratos/Bienes/Respaldos \
    && mkdir -p wwwroot/Uploads/Contratos/Servicios \
    && mkdir -p wwwroot/Uploads/Contratos/Obras \
    && mkdir -p wwwroot/Uploads/Contratos/Consultoria \
    && mkdir -p wwwroot/Uploads/Temp \
    && mkdir -p wwwroot/temp

# Copiar wwwroot desde el build stage (incluye assets)
COPY --from=build /src/ContratosPdfApi/wwwroot ./wwwroot/

# Crear directorios adicionales si no existen
RUN mkdir -p wwwroot/temp

# Configurar variables de entorno para librerías
ENV LD_LIBRARY_PATH="/usr/local/lib:/usr/lib:/lib"
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Verificación final
RUN echo "Verificando instalación final..." \
    && wkhtmltopdf --version \
    && echo "LD_LIBRARY_PATH: $LD_LIBRARY_PATH" \
    && ldconfig -p | grep wkhtmltox || echo "wkhtmltox no encontrado en ldconfig" \
    && find /usr -name "*wkhtmltox*" 2>/dev/null || echo "Búsqueda de archivos wkhtmltox completada"

ENTRYPOINT ["dotnet", "ContratosPdfApi.dll"]
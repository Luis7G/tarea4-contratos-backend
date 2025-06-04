-- SCRIPT PARA RENDER - EJECUTAR MANUALMENTE EN LA BD DE RENDER

-- Crear tabla de usuarios
CREATE TABLE IF NOT EXISTS Usuarios (
    Id SERIAL PRIMARY KEY,
    NombreUsuario VARCHAR(100),
    NombreCompleto VARCHAR(255),
    Email VARCHAR(255),
    FechaCreacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    Activo BOOLEAN DEFAULT true
);

-- Crear tabla de tipos de contrato
CREATE TABLE IF NOT EXISTS TiposContrato (
    Id SERIAL PRIMARY KEY,
    Codigo VARCHAR(50) NOT NULL UNIQUE,
    Nombre VARCHAR(255) NOT NULL,
    Descripcion TEXT,
    Activo BOOLEAN DEFAULT true,
    FechaCreacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Crear tabla de archivos
CREATE TABLE IF NOT EXISTS Archivos (
    Id SERIAL PRIMARY KEY,
    NombreOriginal VARCHAR(500) NOT NULL,
    NombreArchivo VARCHAR(500) NOT NULL,
    RutaArchivo VARCHAR(1000) NOT NULL,
    TipoMIME VARCHAR(100),
    Tamaño BIGINT NOT NULL,
    TipoArchivo VARCHAR(50),
    HashSHA256 VARCHAR(64),
    ContieneFiremaDigital BOOLEAN DEFAULT false,
    InfoFirmaDigital JSONB,
    ArchivoBaseParaComparacion VARCHAR(1000),
    FechaSubida TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UsuarioId INTEGER REFERENCES Usuarios(Id)
);

-- Crear tabla de contratos
CREATE TABLE IF NOT EXISTS Contratos (
    Id SERIAL PRIMARY KEY,
    TipoContratoId INTEGER NOT NULL REFERENCES TiposContrato(Id),
    NumeroContrato VARCHAR(50),
    NombreContratista VARCHAR(255) NOT NULL,
    RucContratista VARCHAR(20) NOT NULL,
    MontoContrato DECIMAL(18,2) NOT NULL,
    FechaFirmaContrato DATE NOT NULL,
    ArchivoPdfGeneradoId INTEGER REFERENCES Archivos(Id),
    UsuarioCreadorId INTEGER REFERENCES Usuarios(Id),
    FechaCreacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FechaActualizacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    Estado VARCHAR(20) DEFAULT 'Activo'
);

-- Crear tabla de detalles de contrato
CREATE TABLE IF NOT EXISTS ContratoDetalles (
    Id SERIAL PRIMARY KEY,
    ContratoId INTEGER NOT NULL REFERENCES Contratos(Id) ON DELETE CASCADE,
    DatosEspecificos JSONB,
    FechaCreacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Crear tabla de relación contrato-archivos
CREATE TABLE IF NOT EXISTS ContratoArchivos (
    Id SERIAL PRIMARY KEY,
    ContratoId INTEGER NOT NULL REFERENCES Contratos(Id) ON DELETE CASCADE,
    ArchivoId INTEGER NOT NULL REFERENCES Archivos(Id) ON DELETE CASCADE,
    FechaAsociacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(ContratoId, ArchivoId)
);

-- Insertar datos iniciales
INSERT INTO Usuarios (NombreUsuario, NombreCompleto, Email) 
VALUES ('admin', 'Administrador del Sistema', 'admin@empresa.com')
ON CONFLICT (NombreUsuario) DO NOTHING;

INSERT INTO TiposContrato (Codigo, Nombre, Descripcion) VALUES
('BIENES', 'Adquisición de Bienes', 'Contratos para adquisición de equipos, materiales y suministros'),
('SERVICIOS', 'Contratación de Servicios', 'Contratos para servicios profesionales y técnicos'),
('OBRAS', 'Ejecución de Obras', 'Contratos para construcción y obras civiles'),
('CONSULTORIA', 'Servicios de Consultoría', 'Contratos para asesorías y consultorías especializadas')
ON CONFLICT (Codigo) DO NOTHING;

-- Función para insertar archivo
CREATE OR REPLACE FUNCTION insertar_archivo(
    p_nombre_original VARCHAR,
    p_nombre_archivo VARCHAR,
    p_ruta_archivo VARCHAR,
    p_tipo_mime VARCHAR,
    p_tamaño BIGINT,
    p_tipo_archivo VARCHAR,
    p_hash_sha256 VARCHAR,
    p_usuario_id INTEGER
) RETURNS INTEGER AS $$
DECLARE
    nuevo_id INTEGER;
BEGIN
    INSERT INTO Archivos (
        NombreOriginal, NombreArchivo, RutaArchivo, TipoMIME, 
        Tamaño, TipoArchivo, HashSHA256, UsuarioId
    ) VALUES (
        p_nombre_original, p_nombre_archivo, p_ruta_archivo, p_tipo_mime,
        p_tamaño, p_tipo_archivo, p_hash_sha256, p_usuario_id
    ) RETURNING Id INTO nuevo_id;
    
    RETURN nuevo_id;
END;
$$ LANGUAGE plpgsql;

-- Función corregida para insertar contrato
CREATE OR REPLACE FUNCTION insertar_contrato(
    p_tipo_contrato_id INTEGER,
    p_numero_contrato VARCHAR,
    p_nombre_contratista VARCHAR,
    p_ruc_contratista VARCHAR,
    p_monto_contrato DECIMAL,
    p_fecha_firma_contrato DATE,
    p_usuario_creador_id INTEGER,
    p_datos_especificos JSONB DEFAULT NULL
) RETURNS INTEGER AS $$
DECLARE
    nuevo_contrato_id INTEGER;
BEGIN
    INSERT INTO Contratos (
        TipoContratoId, NumeroContrato, NombreContratista, RucContratista,
        MontoContrato, FechaFirmaContrato, UsuarioCreadorId, Estado
    ) VALUES (
        p_tipo_contrato_id, p_numero_contrato, p_nombre_contratista, p_ruc_contratista,
        p_monto_contrato, p_fecha_firma_contrato, p_usuario_creador_id, 'Activo'
    ) RETURNING Id INTO nuevo_contrato_id;
    
    -- Insertar datos específicos si existen
    IF p_datos_especificos IS NOT NULL THEN
        INSERT INTO ContratoDetalles (ContratoId, DatosEspecificos)
        VALUES (nuevo_contrato_id, p_datos_especificos);
    END IF;
    
    RETURN nuevo_contrato_id;
END;
$$ LANGUAGE plpgsql;
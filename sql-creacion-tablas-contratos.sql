-- Crear base de datos (opcional, si no existe)
CREATE DATABASE ContratosDB;
USE ContratosDB;

-- Tabla de usuarios gen�rica para pruebas
CREATE TABLE Usuarios (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    NombreUsuario NVARCHAR(50) UNIQUE NOT NULL,
    Email NVARCHAR(255) UNIQUE NOT NULL,
    NombreCompleto NVARCHAR(255) NOT NULL,
    Rol NVARCHAR(50) DEFAULT 'Usuario', -- Admin, Usuario, Supervisor, etc.
    Activo BIT DEFAULT 1,
    FechaCreacion DATETIME2 DEFAULT GETUTCDATE()
);

-- Tabla para tipos de contratos
CREATE TABLE TiposContrato (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Codigo NVARCHAR(20) UNIQUE NOT NULL, -- 'BIENES', 'SERVICIOS', 'OBRAS', etc.
    Nombre NVARCHAR(100) NOT NULL,       -- 'Adquisici�n de Bienes', 'Prestaci�n de Servicios', etc.
    Descripcion NVARCHAR(500) NULL,
    Activo BIT DEFAULT 1,
    FechaCreacion DATETIME2 DEFAULT GETUTCDATE()
);

-- Tabla para metadatos de archivos generales (con campos para validaci�n de firmas digitales)
CREATE TABLE Archivos (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    NombreOriginal NVARCHAR(255) NOT NULL,
    NombreArchivo NVARCHAR(255) NOT NULL,
    RutaArchivo NVARCHAR(500) NOT NULL,
    TipoMIME NVARCHAR(100) NOT NULL,
    Tama�o BIGINT NOT NULL,
    TipoArchivo NVARCHAR(50) NOT NULL,   -- 'PDF_GENERADO', 'TABLA_CANTIDADES', 'RESPALDO_CONTRATANTE', etc.
    
    -- Campos para validaci�n de firmas digitales e integridad
    HashSHA256 NVARCHAR(64) NULL,        -- Hash del archivo original para validar integridad
    ContieneFiremaDigital BIT DEFAULT 0, -- Si el archivo tiene firma digital
    InfoFirmaDigital NVARCHAR(MAX) NULL, -- JSON con informaci�n de las firmas (certificados, fechas, etc.)
    ArchivoBaseParaComparacion NVARCHAR(500) NULL, -- Ruta del archivo sin firmar para comparaci�n
    
    FechaSubida DATETIME2 DEFAULT GETUTCDATE(),
    UsuarioId INT NULL, -- FK a Usuarios
    
    CONSTRAINT FK_Archivos_Usuario FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id)
);

-- Tabla principal para contratos (datos comunes a todos los tipos)
CREATE TABLE Contratos (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TipoContratoId INT NOT NULL,         -- FK a TiposContrato
    
    -- Datos b�sicos del contrato
    NumeroContrato NVARCHAR(50) NULL,    -- N�mero oficial del contrato
    NombreContratista NVARCHAR(255) NOT NULL,
    RucContratista NVARCHAR(20) NOT NULL,
    MontoContrato DECIMAL(18,2) NOT NULL,
    FechaFirmaContrato DATE NOT NULL,
    
    -- Referencias a archivos principales
    ArchivoPdfGeneradoId INT NULL,       -- FK a Archivos (PDF final)
    
    -- Metadatos del sistema
    FechaCreacion DATETIME2 DEFAULT GETUTCDATE(),
    FechaActualizacion DATETIME2 DEFAULT GETUTCDATE(),
    Estado NVARCHAR(50) DEFAULT 'Activo', -- Activo, Finalizado, Cancelado, etc.
    UsuarioCreadorId INT NULL,           -- FK a Usuarios
    
    -- Foreign Keys
    CONSTRAINT FK_Contratos_TipoContrato FOREIGN KEY (TipoContratoId) REFERENCES TiposContrato(Id),
    CONSTRAINT FK_Contratos_PdfGenerado FOREIGN KEY (ArchivoPdfGeneradoId) REFERENCES Archivos(Id),
    CONSTRAINT FK_Contratos_UsuarioCreador FOREIGN KEY (UsuarioCreadorId) REFERENCES Usuarios(Id)
);

-- Tabla de relaci�n entre contratos y archivos (muchos a muchos) - SIMPLIFICADA
CREATE TABLE ContratoArchivos (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ContratoId INT NOT NULL,
    ArchivoId INT NOT NULL,
    FechaAsociacion DATETIME2 DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_ContratoArchivos_Contrato FOREIGN KEY (ContratoId) REFERENCES Contratos(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ContratoArchivos_Archivo FOREIGN KEY (ArchivoId) REFERENCES Archivos(Id),
    CONSTRAINT UQ_ContratoArchivos_Unico UNIQUE (ContratoId, ArchivoId)
);

-- Tabla para datos espec�ficos por tipo de contrato (JSON flexible)
CREATE TABLE ContratoDetalles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ContratoId INT NOT NULL,
    DatosEspecificos NVARCHAR(MAX) NOT NULL, -- JSON con datos espec�ficos del tipo de contrato
    FechaCreacion DATETIME2 DEFAULT GETUTCDATE(),
    FechaActualizacion DATETIME2 DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_ContratoDetalles_Contrato FOREIGN KEY (ContratoId) REFERENCES Contratos(Id) ON DELETE CASCADE
);

-- Insertar datos de prueba

-- Usuarios de prueba
INSERT INTO Usuarios (NombreUsuario, Email, NombreCompleto, Rol) VALUES 
('admin', 'admin@empresa.com', 'Administrador Sistema', 'Admin'),
('jperez', 'jperez@empresa.com', 'Juan P�rez', 'Usuario'),
('mrodriguez', 'mrodriguez@empresa.com', 'Mar�a Rodr�guez', 'Supervisor');

-- Tipos de contrato predeterminados
INSERT INTO TiposContrato (Codigo, Nombre, Descripcion) VALUES 
('BIENES', 'Adquisici�n de Bienes', 'Contratos para la compra de bienes muebles'),
('SERVICIOS', 'Prestaci�n de Servicios', 'Contratos para la prestaci�n de servicios'),
('OBRAS', 'Ejecuci�n de Obras', 'Contratos para la construcci�n y obras civiles'),
('CONSULTORIA', 'Servicios de Consultor�a', 'Contratos para servicios de consultor�a especializada');

-- �ndices para mejorar performance
CREATE INDEX IX_Usuarios_Email ON Usuarios(Email);
CREATE INDEX IX_Usuarios_NombreUsuario ON Usuarios(NombreUsuario);
CREATE INDEX IX_Usuarios_Rol ON Usuarios(Rol);

CREATE INDEX IX_Contratos_TipoContrato ON Contratos(TipoContratoId);
CREATE INDEX IX_Contratos_RucContratista ON Contratos(RucContratista);
CREATE INDEX IX_Contratos_FechaFirma ON Contratos(FechaFirmaContrato);
CREATE INDEX IX_Contratos_Estado ON Contratos(Estado);
CREATE INDEX IX_Contratos_NumeroContrato ON Contratos(NumeroContrato);
CREATE INDEX IX_Contratos_UsuarioCreador ON Contratos(UsuarioCreadorId);

CREATE INDEX IX_Archivos_FechaSubida ON Archivos(FechaSubida);
CREATE INDEX IX_Archivos_TipoArchivo ON Archivos(TipoArchivo);
CREATE INDEX IX_Archivos_HashSHA256 ON Archivos(HashSHA256);
CREATE INDEX IX_Archivos_ContieneFiremaDigital ON Archivos(ContieneFiremaDigital);
CREATE INDEX IX_Archivos_Usuario ON Archivos(UsuarioId);

CREATE INDEX IX_ContratoArchivos_Contrato ON ContratoArchivos(ContratoId);

-- Triggers para actualizar fechas autom�ticamente
CREATE TRIGGER TR_Contratos_UpdateTimestamp
ON Contratos
AFTER UPDATE
AS
BEGIN
    UPDATE Contratos 
    SET FechaActualizacion = GETUTCDATE()
    WHERE Id IN (SELECT Id FROM inserted);
END;

CREATE TRIGGER TR_ContratoDetalles_UpdateTimestamp
ON ContratoDetalles
AFTER UPDATE
AS
BEGIN
    UPDATE ContratoDetalles 
    SET FechaActualizacion = GETUTCDATE()
    WHERE Id IN (SELECT Id FROM inserted);
END;



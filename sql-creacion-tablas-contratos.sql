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
INSERT INTO TiposContrato (Codigo, Nombre, Descripcion, Activo) VALUES 
('BIENES', 'Adquisición de Bienes', 'Contratos para la compra de bienes muebles', 1),
('OBRAS', 'Contratación de Obras', 'Contratos para ejecución de obras de construcción', 1),
('SERVICIOS', 'Contratación de Servicios', 'Contratos para prestación de servicios generales', 1),
('CONSULTORIA', 'Contratación de Consultoría', 'Contratos para servicios de consultoría especializada', 1),
('TRANSPORTE', 'Servicios de Transporte de Materiales', 'Contratos para transporte de materiales', 1);

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
CREATE INDEX IX_Archivos_ContieneFiremaDigital ON Archivos(ContieneFiremaDigital);
CREATE INDEX IX_Archivos_HashSHA256 ON Archivos(HashSHA256);
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


-- Crear tabla para tipos de archivos adjuntos específicos
CREATE TABLE TiposArchivosAdjuntos (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Codigo NVARCHAR(50) UNIQUE NOT NULL,
    Nombre NVARCHAR(255) NOT NULL,
    Descripcion NVARCHAR(500) NULL,
    Categoria NVARCHAR(50) NOT NULL, -- 'CONTRATANTE', 'CONTRATISTA', 'GENERAL', 'GARANTIAS'
    EsObligatorio BIT DEFAULT 0,
    Activo BIT DEFAULT 1,
    FechaCreacion DATETIME2 DEFAULT GETUTCDATE()
);

-- Insertar tipos de archivos adjuntos
INSERT INTO TiposArchivosAdjuntos (Codigo, Nombre, Descripcion, Categoria, EsObligatorio) VALUES
-- Contratante - Gerente General
('CONTRATANTE_NOMBRAMIENTO_HEH', 'Nombramiento HeH Vigente', 'Nombramiento vigente de Hidroeléctrica El Hermano', 'CONTRATANTE', 1),
('CONTRATANTE_NOMBRAMIENTO_GEMADEMSA', 'Nombramiento Gemademsa Vigente', 'Nombramiento vigente de Gemademsa', 'CONTRATANTE', 1),
('CONTRATANTE_CEDULA', 'Cédula Vigente', 'Cédula de identidad vigente del representante', 'CONTRATANTE', 1),
('CONTRATANTE_PAPELETA', 'Papeleta de Votación Vigente', 'Papeleta de votación vigente', 'CONTRATANTE', 1),
('CONTRATANTE_RUC_HEH', 'RUC HeH', 'RUC de Hidroeléctrica El Hermano', 'CONTRATANTE', 1),
('CONTRATANTE_RUC_GEMADEMSA', 'RUC Gemademsa', 'RUC de Gemademsa', 'CONTRATANTE', 1),

-- Contratante - Apoderado Especial
('CONTRATANTE_PODER_ESPECIAL', 'Poder Especial', 'Poder especial del apoderado', 'CONTRATANTE', 1),

-- Contratante - Superintendente
('CONTRATANTE_DESIGNACION', 'Designación', 'Designación del superintendente', 'CONTRATANTE', 1),
('CONTRATANTE_SUMILLA_APROBACION', 'Sumilla de Aprobación', 'Sumilla de aprobación para contratos mayores a 10 mil', 'CONTRATANTE', 0),

-- Contratista - Persona Jurídica - Gerente General
('CONTRATISTA_NOMBRAMIENTO', 'Nombramiento Contratista Vigente', 'Nombramiento vigente del contratista', 'CONTRATISTA', 1),
('CONTRATISTA_CEDULA', 'Cédula Vigente', 'Cédula de identidad vigente', 'CONTRATISTA', 1),
('CONTRATISTA_PAPELETA', 'Papeleta de Votación Vigente', 'Papeleta de votación vigente', 'CONTRATISTA', 1),
('CONTRATISTA_RUC', 'RUC Contratista', 'RUC del contratista', 'CONTRATISTA', 1),

-- Contratista - Apoderado Especial
('CONTRATISTA_PODER_ESPECIAL', 'Poder Especial', 'Poder especial del contratista', 'CONTRATISTA', 1),

-- General
('OFERTA_VIGENTE', 'Oferta Vigente', 'Oferta vigente presentada y aprobada', 'GENERAL', 1),

-- Garantías
('POLIZA_FIEL_CUMPLIMIENTO', 'Póliza Fiel Cumplimiento', 'Póliza o garantía bancaria de fiel cumplimiento del contrato', 'GARANTIAS', 0),
('POLIZA_BUEN_USO_ANTICIPO', 'Póliza Buen Uso Anticipo', 'Póliza o garantía bancaria del buen uso de anticipo', 'GARANTIAS', 0),
('GARANTIA_TECNICA', 'Garantía Técnica', 'Garantía técnica de bienes (obligatorio)', 'GARANTIAS', 1);

-- Crear tabla de archivos adjuntos específicos por contrato
CREATE TABLE ContratoArchivosAdjuntos (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ContratoId INT NOT NULL,
    TipoArchivoAdjuntoId INT NOT NULL,
    ArchivoId INT NOT NULL,
    EsObligatorio BIT DEFAULT 0,
    FechaSubida DATETIME2 DEFAULT GETUTCDATE(),
    UsuarioId INT NULL,
    
    CONSTRAINT FK_ContratoArchivosAdjuntos_Contrato FOREIGN KEY (ContratoId) REFERENCES Contratos(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ContratoArchivosAdjuntos_TipoArchivo FOREIGN KEY (TipoArchivoAdjuntoId) REFERENCES TiposArchivosAdjuntos(Id),
    CONSTRAINT FK_ContratoArchivosAdjuntos_Archivo FOREIGN KEY (ArchivoId) REFERENCES Archivos(Id),
    CONSTRAINT FK_ContratoArchivosAdjuntos_Usuario FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id),
    CONSTRAINT UQ_ContratoArchivosAdjuntos_Unico UNIQUE (ContratoId, TipoArchivoAdjuntoId, ArchivoId)
);

-- Agregar campos faltantes a ContratoArchivosAdjuntos
ALTER TABLE ContratoArchivosAdjuntos 
ADD NombreOriginal NVARCHAR(255) NULL,
    NombreArchivo NVARCHAR(255) NULL,
    RutaArchivo NVARCHAR(500) NULL,
    TipoMIME NVARCHAR(100) NULL,
    Tamaño BIGINT NULL,
    HashSHA256 NVARCHAR(64) NULL;

-- Crear índices
CREATE INDEX IX_ContratoArchivosAdjuntos_Tipo ON ContratoArchivosAdjuntos(TipoArchivoAdjuntoId);

-- Stored Procedures
SP_ActualizarPdfContrato	
-- SP para actualizar PDF generado en contrato
CREATE PROCEDURE SP_ActualizarPdfContrato
    @ContratoId INT,
    @ArchivoPdfGeneradoId INT
AS
BEGIN
    UPDATE Contratos 
    SET ArchivoPdfGeneradoId = @ArchivoPdfGeneradoId,
        FechaActualizacion = GETUTCDATE()
    WHERE Id = @ContratoId;
END;
SP_AsociarArchivoAdjuntoContrato	CREATE PROCEDURE SP_AsociarArchivoAdjuntoContrato
    @ContratoId INT,
    @TipoArchivoAdjuntoId INT,
    @ArchivoId INT,
    @UsuarioId INT = NULL
AS
BEGIN
    INSERT INTO ContratoArchivosAdjuntos (ContratoId, TipoArchivoAdjuntoId, ArchivoId, UsuarioId)
    VALUES (@ContratoId, @TipoArchivoAdjuntoId, @ArchivoId, @UsuarioId);
    
    SELECT SCOPE_IDENTITY() as Id;
END;
SP_AsociarArchivoContrato	
-- SP para asociar archivo a contrato
CREATE PROCEDURE SP_AsociarArchivoContrato
    @ContratoId INT,
    @ArchivoId INT
AS
BEGIN
    IF NOT EXISTS (SELECT 1 FROM ContratoArchivos WHERE ContratoId = @ContratoId AND ArchivoId = @ArchivoId)
    BEGIN
        INSERT INTO ContratoArchivos (ContratoId, ArchivoId)
        VALUES (@ContratoId, @ArchivoId);
    END;
END;
SP_InsertarArchivo	-- SP para insertar archivo
CREATE PROCEDURE SP_InsertarArchivo
    @NombreOriginal NVARCHAR(255),
    @NombreArchivo NVARCHAR(255),
    @RutaArchivo NVARCHAR(500),
    @TipoMIME NVARCHAR(100),
    @Tamaño BIGINT,
    @TipoArchivo NVARCHAR(50),
    @HashSHA256 NVARCHAR(64) = NULL,
    @UsuarioId INT = NULL
AS
BEGIN
    INSERT INTO Archivos (NombreOriginal, NombreArchivo, RutaArchivo, TipoMIME, Tamaño, TipoArchivo, HashSHA256, ContieneFiremaDigital, UsuarioId)
    VALUES (@NombreOriginal, @NombreArchivo, @RutaArchivo, @TipoMIME, @Tamaño, @TipoArchivo, @HashSHA256, 0, @UsuarioId);
    
    SELECT SCOPE_IDENTITY() AS ArchivoId;
END;
SP_InsertarContrato	
-- SP para insertar contrato
CREATE PROCEDURE SP_InsertarContrato
    @TipoContratoId INT,
    @NumeroContrato NVARCHAR(50) = NULL,
    @NombreContratista NVARCHAR(255),
    @RucContratista NVARCHAR(20),
    @MontoContrato DECIMAL(18,2),
    @FechaFirmaContrato DATE,
    @UsuarioCreadorId INT = NULL,
    @DatosEspecificos NVARCHAR(MAX) = NULL
AS
BEGIN
    DECLARE @ContratoId INT;
    
    -- Insertar contrato
    INSERT INTO Contratos (TipoContratoId, NumeroContrato, NombreContratista, RucContratista, MontoContrato, FechaFirmaContrato, UsuarioCreadorId)
    VALUES (@TipoContratoId, @NumeroContrato, @NombreContratista, @RucContratista, @MontoContrato, @FechaFirmaContrato, @UsuarioCreadorId);
    
    SET @ContratoId = SCOPE_IDENTITY();
    
    -- Insertar detalles si se proporcionan
    IF @DatosEspecificos IS NOT NULL
    BEGIN
        INSERT INTO ContratoDetalles (ContratoId, DatosEspecificos)
        VALUES (@ContratoId, @DatosEspecificos);
    END;
    
    SELECT @ContratoId AS ContratoId;
END;
SP_ListarContratos	
-- SP para listar contratos
CREATE PROCEDURE SP_ListarContratos
    @TipoContratoId INT = NULL,
    @Estado NVARCHAR(50) = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 10
AS
BEGIN
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    
    SELECT c.*, tc.Codigo as TipoContratoCodigo, tc.Nombre as TipoContratoNombre,
           u.NombreCompleto as UsuarioCreadorNombre
    FROM Contratos c
    INNER JOIN TiposContrato tc ON c.TipoContratoId = tc.Id
    LEFT JOIN Usuarios u ON c.UsuarioCreadorId = u.Id
    WHERE (@TipoContratoId IS NULL OR c.TipoContratoId = @TipoContratoId)
      AND (@Estado IS NULL OR c.Estado = @Estado)
    ORDER BY c.FechaCreacion DESC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;
    
    -- Total de registros
    SELECT COUNT(*) as Total
    FROM Contratos c
    WHERE (@TipoContratoId IS NULL OR c.TipoContratoId = @TipoContratoId)
      AND (@Estado IS NULL OR c.Estado = @Estado);
END;
SP_ObtenerArchivoPorId	-- SP para obtener archivo por ID
CREATE PROCEDURE SP_ObtenerArchivoPorId
    @ArchivoId INT
AS
BEGIN
    SELECT * FROM Archivos WHERE Id = @ArchivoId;
END;
	
CREATE PROCEDURE SP_ObtenerArchivosAdjuntosPorContrato
    @ContratoId INT
AS
BEGIN
    SELECT 
        caa.Id,
        caa.ContratoId,
        taa.Codigo as TipoArchivoCodigo,
        taa.Nombre as TipoArchivoNombre,
        taa.Categoria,
        taa.EsObligatorio,
        a.Id as ArchivoId,
        a.NombreOriginal,
        a.NombreArchivo,
        a.RutaArchivo,
        a.TipoMIME,
        a.Tamaño,
        caa.FechaSubida
    FROM ContratoArchivosAdjuntos caa
    INNER JOIN TiposArchivosAdjuntos taa ON caa.TipoArchivoAdjuntoId = taa.Id
    INNER JOIN Archivos a ON caa.ArchivoId = a.Id
    WHERE caa.ContratoId = @ContratoId
    ORDER BY taa.Categoria, taa.Nombre;
END;
SP_ObtenerContratoPorId	
-- SP para obtener contrato completo
CREATE PROCEDURE SP_ObtenerContratoPorId
    @ContratoId INT
AS
BEGIN
    -- Datos del contrato
    SELECT c.*, tc.Codigo as TipoContratoCodigo, tc.Nombre as TipoContratoNombre,
           u.NombreCompleto as UsuarioCreadorNombre
    FROM Contratos c
    INNER JOIN TiposContrato tc ON c.TipoContratoId = tc.Id
    LEFT JOIN Usuarios u ON c.UsuarioCreadorId = u.Id
    WHERE c.Id = @ContratoId;
    
    -- Archivos asociados
    SELECT a.*, ca.FechaAsociacion
    FROM Archivos a
    INNER JOIN ContratoArchivos ca ON a.Id = ca.ArchivoId
    WHERE ca.ContratoId = @ContratoId;
    
    -- Detalles específicos
    SELECT DatosEspecificos FROM ContratoDetalles WHERE ContratoId = @ContratoId;
END;
SP_ObtenerTiposArchivosAdjuntos	CREATE PROCEDURE SP_ObtenerTiposArchivosAdjuntos
    @Categoria NVARCHAR(50) = NULL
AS
BEGIN
    SELECT Id, Codigo, Nombre, Descripcion, Categoria, EsObligatorio
    FROM TiposArchivosAdjuntos
    WHERE Activo = 1
    AND (@Categoria IS NULL OR Categoria = @Categoria)
    ORDER BY Categoria, Nombre;
END;
SP_ObtenerTiposContrato	
-- SP para obtener tipos de contrato
CREATE PROCEDURE SP_ObtenerTiposContrato
AS
BEGIN
    SELECT * FROM TiposContrato WHERE Activo = 1 ORDER BY Nombre;
END;
SP_ObtenerUsuarios	
-- SP para obtener usuarios activos
CREATE PROCEDURE SP_ObtenerUsuarios
AS
BEGIN
    SELECT Id, NombreUsuario, Email, NombreCompleto, Rol 
    FROM Usuarios 
    WHERE Activo = 1 
    ORDER BY NombreCompleto;
END;
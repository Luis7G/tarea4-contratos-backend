using System.Data;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace ContratosPdfApi.Services
{
    public interface IDatabaseHelper
    {
        IDbConnection CreateConnection();
        string GetInsertArchiveSql();
        string GetInsertContratoSql();
        bool IsPostgreSQL { get; }
    }

    public class DatabaseHelper : IDatabaseHelper
    {
        private readonly string _connectionString;
        private readonly string _provider;
        private readonly ILogger<DatabaseHelper> _logger;

        public DatabaseHelper(IConfiguration configuration, ILogger<DatabaseHelper> logger)
        {
            _logger = logger;
            _provider = configuration["DatabaseProvider"] ?? "SqlServer";

            if (_provider == "PostgreSQL")
            {
                // En producción (Render), usar DATABASE_URL
                var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
                if (!string.IsNullOrEmpty(databaseUrl))
                {
                    _connectionString = ParseRenderDatabaseUrl(databaseUrl);
                    _logger.LogInformation($"🐘 Usando DATABASE_URL de Render");
                }
                else
                {
                    // Desarrollo local
                    _connectionString = configuration.GetConnectionString("PostgreSQLConnection")!;
                    _logger.LogInformation($"🐘 Usando PostgreSQL local");
                }
            }
            else
            {
                _connectionString = configuration.GetConnectionString("DefaultConnection")!;
                _logger.LogInformation($"🏠 Usando SQL Server local");
            }

            // Log de la conexión (sin mostrar password)
            var safeConnectionString = _connectionString?.Contains("Password=") == true
                ? _connectionString.Substring(0, _connectionString.IndexOf("Password=")) + "Password=***"
                : _connectionString?.Substring(0, Math.Min(50, _connectionString?.Length ?? 0)) + "...";

            _logger.LogInformation($"📡 Connection String: {safeConnectionString}");
        }

        private string ParseRenderDatabaseUrl(string databaseUrl)
        {
            try
            {
                _logger.LogInformation($"🔍 Parsing DATABASE_URL: {databaseUrl.Substring(0, Math.Min(30, databaseUrl.Length))}...");

                var uri = new Uri(databaseUrl);

                // Extraer componentes
                var host = uri.Host;
                var port = uri.Port > 0 ? uri.Port : 5432; // Default PostgreSQL port
                var database = uri.AbsolutePath.TrimStart('/');
                var userInfo = uri.UserInfo.Split(':');
                var username = userInfo[0];
                var password = userInfo.Length > 1 ? userInfo[1] : "";

                // Construir connection string para Npgsql
                var connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true;Timeout=30;Command Timeout=30;";

                _logger.LogInformation($"✅ PostgreSQL parsed - Host: {host}, Port: {port}, DB: {database}, User: {username}");

                return connectionString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error parsing DATABASE_URL: {ex.Message}");
                throw new InvalidOperationException($"Error al parsear DATABASE_URL: {ex.Message}", ex);
            }
        }

        public bool IsPostgreSQL => _provider == "PostgreSQL";

        public IDbConnection CreateConnection()
        {
            try
            {
                if (_provider == "PostgreSQL")
                {
                    var connection = new NpgsqlConnection(_connectionString);
                    _logger.LogDebug($"🔗 Creando conexión PostgreSQL");
                    return connection;
                }
                else
                {
                    var connection = new SqlConnection(_connectionString);
                    _logger.LogDebug($"🔗 Creando conexión SQL Server");
                    return connection;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error creando conexión de BD: {ex.Message}");
                throw;
            }
        }

        public string GetInsertArchiveSql()
        {
            if (_provider == "PostgreSQL")
            {
                return "SELECT insertar_archivo(@NombreOriginal, @NombreArchivo, @RutaArchivo, @TipoMIME, @Tamaño, @TipoArchivo, @HashSHA256, @UsuarioId)";
            }
            else
            {
                return "SP_InsertarArchivo";
            }
        }

        public string GetInsertContratoSql()
        {
            if (_provider == "PostgreSQL")
            {
                return "SELECT insertar_contrato(@TipoContratoId, @NumeroContrato, @NombreContratista, @RucContratista, @MontoContrato, @FechaFirmaContrato, @UsuarioCreadorId, @DatosEspecificos::jsonb)";
            }
            else
            {
                return "SP_InsertarContrato";
            }
        }
    }
}
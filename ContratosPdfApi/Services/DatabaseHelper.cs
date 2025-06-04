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

        public DatabaseHelper(IConfiguration configuration)
        {
            _provider = configuration["DatabaseProvider"] ?? "SqlServer";
            
            if (_provider == "PostgreSQL")
            {
                _connectionString = configuration.GetConnectionString("PostgreSQLConnection")!;
                
                // En producción (Render), usar DATABASE_URL
                var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
                if (!string.IsNullOrEmpty(databaseUrl))
                {
                    var uri = new Uri(databaseUrl);
                    _connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={uri.UserInfo.Split(':')[0]};Password={uri.UserInfo.Split(':')[1]};SSL Mode=Require;Trust Server Certificate=true;";
                }
            }
            else
            {
                _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            }
        }

        public bool IsPostgreSQL => _provider == "PostgreSQL";

        public IDbConnection CreateConnection()
        {
            if (_provider == "PostgreSQL")
                return new NpgsqlConnection(_connectionString);
            else
                return new SqlConnection(_connectionString);
        }

        public string GetInsertArchiveSql()
        {
            if (_provider == "PostgreSQL")
            {
                // PostgreSQL: Usar función
                return "SELECT insertar_archivo(@NombreOriginal, @NombreArchivo, @RutaArchivo, @TipoMIME, @Tamaño, @TipoArchivo, @HashSHA256, @UsuarioId)";
            }
            else
            {
                // SQL Server: Usar stored procedure
                return "SP_InsertarArchivo";
            }
        }

        public string GetInsertContratoSql()
        {
            if (_provider == "PostgreSQL")
            {
                // PostgreSQL: Usar función
                return "SELECT insertar_contrato(@TipoContratoId, @NumeroContrato, @NombreContratista, @RucContratista, @MontoContrato, @FechaFirmaContrato, @UsuarioCreadorId, @DatosEspecificos::jsonb)";
            }
            else
            {
                // SQL Server: Usar stored procedure
                return "SP_InsertarContrato";
            }
        }
    }
}
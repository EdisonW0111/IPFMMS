using System.Data;
using IPFMMS.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IPFMMS.Services
{
    public class IdGeneratorService
    {
        private readonly ApplicationDbContext _context;

        public IdGeneratorService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateIdAsync(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix) ||
                prefix.Length > 10 ||
                !prefix.All(c => c >= 'A' && c <= 'Z'))
            {
                throw new ArgumentException("Invalid ID prefix.");
            }

            bool openedHere =
                _context.Database.GetDbConnection().State
                != ConnectionState.Open;

            if (openedHere)
                await _context.Database.OpenConnectionAsync();

            try
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $@"INSERT INTO IdCounters (Prefix, LastNumber)
                       VALUES ({prefix}, LAST_INSERT_ID(1))
                       ON DUPLICATE KEY UPDATE
                       LastNumber = LAST_INSERT_ID(LastNumber + 1)");

                var connection = _context.Database.GetDbConnection();

                await using var command = connection.CreateCommand();

                command.CommandText = "SELECT LAST_INSERT_ID();";

                // Use the active EF Core transaction, if one exists.
                var transaction =
                    _context.Database.CurrentTransaction;

                if (transaction != null)
                {
                    command.Transaction =
                        transaction.GetDbTransaction();
                }

                var result = await command.ExecuteScalarAsync();

                long number = Convert.ToInt64(result);

                return $"{prefix}{number:D5}";
            }
            finally
            {
                if (openedHere)
                    await _context.Database.CloseConnectionAsync();
            }
        }
    }
}
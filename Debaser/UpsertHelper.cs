using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Debaser.Internals;
using Debaser.Internals.Data;
using Debaser.Internals.Query;
using Debaser.Internals.Schema;
using Debaser.Mapping;
using Microsoft.SqlServer.Server;
using Activator = Debaser.Internals.Reflection.Activator;

namespace Debaser
{
	/// <summary>
	/// This is the UpsertHelper. <code>new</code> up an instance of this guy and start messing around with your data
	/// </summary>
	public class UpsertHelper<T>
	{
		private readonly Activator _activator;
		private readonly ClassMap _classMap;
		private readonly string _connectionString;
		private readonly SchemaManager _schemaManager;
		private readonly Settings _settings;

		/// <summary>
		/// Creates the upsert helper
		/// </summary>
		public UpsertHelper(string connectionString, string tableName = null, string schema = "dbo", Settings settings = null)
			: this(connectionString, AutoMapper.Default.GetMap<T>(), tableName, schema, settings)
		{
		}

		/// <summary>
		/// Creates the upsert helper
		/// </summary>
		public UpsertHelper(string connectionString, ClassMap classMap, string tableName = null, string schema = "dbo", Settings settings = null)
		{
			_connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
			_classMap = classMap ?? throw new ArgumentNullException(nameof(classMap));
			_settings = settings ?? new Settings();

			var upsertTableName = tableName ?? typeof(T).Name;
			var dataTypeName = $"{upsertTableName}Type";
			var procedureName = $"{upsertTableName}Upsert";

			_schemaManager = GetSchemaCreator(schema, upsertTableName, dataTypeName, procedureName);

			_activator = new Activator(typeof(T), _classMap.Properties.Select(p => p.PropertyName));
		}

		/// <summary>
		/// Ensures that the necessary schema is created (i.e. table, custom data type, and stored procedure).
		/// Does NOT detect changes, just skips creation if it finds objects with the known names in the database.
		/// This means that you need to handle migrations yourself
		/// </summary>
		public void CreateSchema(bool createProcedure = true, bool createType = true, bool createTable = true)
		{
			_schemaManager.CreateSchema(createProcedure, createType, createTable);
		}

		/// <summary>
		/// Deletes all rows that match the given criteria. The <paramref name="criteria"/> must be specified on the form
		/// <code>[someColumn] = @someValue</code> where the accompanying <paramref name="args"/> would be something like
		/// <code>new { someValue = "hej" }</code>
		/// </summary>
		public Task DeleteWhere(string criteria, object args = null, SqlTransaction transaction = null)
		{
			if (criteria == null)
				throw new ArgumentNullException(nameof(criteria));

			return Process(transaction, async (connection, txn) => {
				using var command = connection.CreateCommand();
				var querySql = _schemaManager.GetDeleteCommand(criteria);
				var parameters = GetParameters(args);

				command.Transaction = txn;
				command.CommandTimeout = _settings.CommandTimeoutSeconds;
				command.CommandType = CommandType.Text;
				command.CommandText = querySql;
				parameters.ForEach(parameter => parameter.AddTo(command));

				try
				{
					return await command.ExecuteNonQueryAsync();
				}
				catch (Exception exception)
				{
					throw new InvalidOperationException($"Could not execute SQL {querySql}", exception);
				}
			});
		}

		/// <summary>
		/// Immediately executes DROP statements for the things you select by setting <paramref name="dropProcedure"/>,
		/// <paramref name="dropType"/>, and/or <paramref name="dropTable"/> to <code>true</code>.
		/// </summary>
		public void DropSchema(bool dropProcedure = false, bool dropType = false, bool dropTable = false)
		{
			_schemaManager.DropSchema(dropProcedure, dropType, dropTable);
		}

		/// <summary>
		/// Loads all rows from the database (in a streaming fashion, allows you to traverse all
		/// objects without worrying about memory usage)
		/// </summary>
		public List<T> LoadAll()
		{
			using var connection = new SqlConnection(_connectionString);
			connection.Open();
			using var transaction = connection.BeginTransaction(_settings.TransactionIsolationLevel);
			using var command = connection.CreateCommand();
			command.Transaction = transaction;
			command.CommandTimeout = _settings.CommandTimeoutSeconds;
			command.CommandType = CommandType.Text;
			command.CommandText = _schemaManager.GetQuery();

			using var reader = command.ExecuteReader();
			var classMapProperties = _classMap.Properties.ToDictionary(p => p.PropertyName);
			var lookup = new DataReaderLookup(reader, classMapProperties);
			var results = new List<T>();
			while (reader.Read())
				results.Add((T)_activator.CreateInstance(lookup));
			return results;
		}

		/// <summary>
		/// Loads all rows that match the given criteria. The <paramref name="criteria"/> must be specified on the form
		/// <code>[someColumn] = @someValue</code> where the accompanying <paramref name="args"/> would be something like
		/// <code>new { someValue = "hej" }</code>
		/// </summary>
		public async Task<List<T>> LoadAsync(string criteria = null, object args = null)
		{
			return await Process(null, async (connection, txn) => {
				var results = new List<T>();
				using var command = connection.CreateCommand();
				command.Transaction = txn;
				command.CommandTimeout = _settings.CommandTimeoutSeconds;
				command.CommandType = CommandType.Text;

				var querySql = _schemaManager.GetQuery(criteria);
				var parameters = GetParameters(args);
				parameters.ForEach(parameter => parameter.AddTo(command));
				command.CommandText = querySql;

				using var reader = await command.ExecuteReaderAsync();
				var classMapProperties = _classMap.Properties.ToDictionary(p => p.PropertyName);
				var lookup = new DataReaderLookup(reader, classMapProperties);

				while (reader.Read())
					results.Add((T)_activator.CreateInstance(lookup));

				return results;
			});
		}

		public async Task<List<T>> LoadAsync(Expression<Func<T, bool>> predicate)
		{
			return await Process(null, async (connection, txn) => {
				var results = new List<T>();
				using var command = connection.CreateCommand();
				command.Transaction = txn;
				command.CommandTimeout = _settings.CommandTimeoutSeconds;
				command.CommandType = CommandType.Text;
				var criteria = WhereBuilder.ToSql(predicate);
				var querySql = _schemaManager.GetQuery(criteria.Sql);
				var parameters = criteria.GetParameters();
				parameters.ForEach(parameter => parameter.AddTo(command));
				command.CommandText = querySql;

				using var reader = await command.ExecuteReaderAsync();
				var classMapProperties = _classMap.Properties.ToDictionary(p => p.PropertyName);
				var lookup = new DataReaderLookup(reader, classMapProperties);

				while (reader.Read())
					results.Add((T)_activator.CreateInstance(lookup));

				return results;
			});
		}

		/// <summary>
		/// Upserts the given sequence of <typeparamref name="T"/> instances
		/// </summary>
		/// <param name="models">Models to upsert into database</param>
		/// <param name="transaction">Transaction</param>
		public async ValueTask<int> Modify(IEnumerable<T> models, SqlTransaction transaction = null)
		{
			if (models == null || !models.Any())
				return 0;

			return await Process(transaction, async (connection, txn) => {
				using var command = connection.CreateCommand();
				command.Transaction = txn;
				command.CommandTimeout = _settings.CommandTimeoutSeconds;
				command.CommandType = CommandType.StoredProcedure;
				command.CommandText = _schemaManager.SprocName;

				var parameter = command.Parameters.AddWithValue("data", GetData(models));
				parameter.SqlDbType = SqlDbType.Structured;
				parameter.TypeName = _schemaManager.DataTypeName;

				return await command.ExecuteNonQueryAsync();
			});
		}

		private IEnumerable<SqlDataRecord> GetData(IEnumerable<T> rows)
		{
			var sqlMetaData = _classMap.GetSqlMetaData();
			var reusableRecord = new SqlDataRecord(sqlMetaData);

			foreach (var row in rows)
			{
				foreach (var property in _classMap.Properties)
				{
					try
					{
						property.WriteTo(reusableRecord, row);
					}
					catch (Exception exception)
					{
						throw new InvalidOperationException($"Could not write property {property} of row {row}", exception);
					}
				}

				yield return reusableRecord;
			}
		}

		private List<Parameter> GetParameters(object args)
		{
			if (args == null)
				return new List<Parameter>();

			var properties = args.GetType().GetProperties();

			return properties
				.Select(p => new Parameter(p.Name, p.GetValue(args)))
				.ToList();
		}

		private SchemaManager GetSchemaCreator(string schema, string tableName, string dataTypeName, string procedureName)
		{
			var properties = _classMap.Properties.ToList();
			var keyProperties = properties.Where(p => p.IsKey);
			var extraCriteria = _classMap.GetExtraCriteria();

			return new SchemaManager(_connectionString, tableName, dataTypeName, procedureName, keyProperties, properties, schema, extraCriteria);
		}

		private async Task<TResult> Process<TResult>(SqlTransaction transaction, Func<SqlConnection, SqlTransaction, Task<TResult>> process)
		{
			var result = default(TResult);
			var exists = transaction?.Connection != null;
			var connection = transaction?.Connection ?? new SqlConnection(_connectionString);
			if (connection.State != ConnectionState.Open)
				await connection.OpenAsync();

			try
			{
				transaction ??= connection.BeginTransaction(_settings.TransactionIsolationLevel);
				result = await process(connection, transaction);
				if (!exists)
					transaction.Commit();
			}
			catch
			{
				if (!exists)
					transaction.Rollback();
				else
					throw;
			}
			finally
			{
				if (!exists)
				{
					try
					{
						transaction.Dispose();
						await connection.CloseAsync();
						connection.Dispose();
					}
					catch
					{
						// do nothing
					}
				}
			}
			return result;
		}
	}
}
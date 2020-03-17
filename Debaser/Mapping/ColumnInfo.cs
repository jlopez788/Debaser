﻿using Microsoft.SqlServer.Server;
using System;
using System.Data;
using System.Linq;

// ReSharper disable ArgumentsStyleNamedExpression

namespace Debaser.Mapping
{
	/// <summary>
	/// Represents a single column in the database
	/// </summary>
	public class ColumnInfo
	{
		/// <summary>
		/// Gets the default length of NVARCHAR columns
		/// </summary>
		public const int DefaultNVarcharLength = 256;

		/// <summary>
		/// Creates the column info
		/// </summary>
		public ColumnInfo(SqlDbType sqlDbType, int? size = null, int? addSize = null, Func<object, object> customToDatabase = null, Func<object, object> customFromDatabase = null)
		{
			SqlDbType = sqlDbType;
			Size = size;
			AddSize = addSize;
			CustomToDatabase = customToDatabase;
			CustomFromDatabase = customFromDatabase;
		}

		/// <summary>
		/// Gets the SQL database column type
		/// </summary>
		public SqlDbType SqlDbType { get; }

		/// <summary>
		/// Gets the size (or null if irrelevant)
		/// </summary>
		public int? Size { get; }

		/// <summary>
		/// Gets the additional size (or null if irrelevant)
		/// Can be used to specify decimal places in the DECIMAL size specification
		/// </summary>
		public int? AddSize { get; }

		internal Func<object, object> CustomToDatabase { get; }

		internal Func<object, object> CustomFromDatabase { get; }

		internal string GetTypeDefinition()
		{
			if (Size == null)
			{
				if (IsString)
				{
					return $"{SqlDbType.ToString().ToUpper()}({DefaultNVarcharLength})";
				}
				return $"{SqlDbType.ToString().ToUpper()}";
			}

			var sizeString = GetSizeString(Size.Value);

			return $"{SqlDbType.ToString().ToUpper()}({sizeString})";
		}

		private string GetSizeString(int size)
		{
			return SizeIsMax(size) ? "MAX" : size.ToString();
		}

		internal SqlMetaData GetSqlMetaData(string columnName)
		{
			if (!Size.HasValue || SizeIsMax(Size.Value))
			{
				if (IsString)
				{
					return new SqlMetaData(columnName, SqlDbType, maxLength: DefaultNVarcharLength);
				}

				return new SqlMetaData(columnName, SqlDbType);
			}

			return new SqlMetaData(columnName, SqlDbType, maxLength: Size.Value);
		}

		private static bool SizeIsMax(int size)
		{
			return size == int.MaxValue;
		}

		private bool IsString => new[] { SqlDbType.NVarChar, SqlDbType.VarChar }.Contains(SqlDbType);
	}
}
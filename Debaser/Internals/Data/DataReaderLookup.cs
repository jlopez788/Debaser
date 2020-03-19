using Debaser.Internals.Values;
using Debaser.Mapping;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace Debaser.Internals.Data
{
	internal class DataReaderLookup : IValueLookup
	{
		private readonly SqlDataReader _reader;
		private readonly Dictionary<string, ClassMapProperty> _properties;

		public DataReaderLookup(SqlDataReader reader, Dictionary<string, ClassMapProperty> properties)
		{
			_reader = reader;
			_properties = new Dictionary<string, ClassMapProperty>(properties, StringComparer.CurrentCultureIgnoreCase);
		}

		public object GetValue(string name, Type desiredType)
		{
			var ordinal = _reader.GetOrdinal(name);
			var value = _reader.GetValue(ordinal);

			var property = GetProperty(name);

			try
			{
				return property.FromDatabase(value);
			}
			catch (Exception exception)
			{
				throw new InvalidOperationException($"Could not get value {name}", exception);
			}
		}

		private ClassMapProperty GetProperty(string name)
		{
			try
			{
				return _properties[name];
			}
			catch (Exception exception)
			{
				throw new InvalidOperationException($"Could not get property named {name} - have these properties: {string.Join(", ", _properties.Select(p => p.Key))}", exception);
			}
		}
	}
}
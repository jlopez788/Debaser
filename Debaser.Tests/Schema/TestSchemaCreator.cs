﻿using System.Linq;
using Debaser.Internals.Schema;
using Debaser.Mapping;
using NUnit.Framework;

namespace Debaser.Tests.Schema
{
	[TestFixture]
	public class TestSchemaCreator : FixtureBase
	{
		[Test]
		public void CanCreateSchema()
		{
			var mapper = new AutoMapper();
			var map = mapper.GetMap(typeof(SomeClass));
			var properties = map.Properties;
			var keyProperties = properties.Where(p => p.IsKey);

			var creator = new SchemaManager(ConnectionString, "testtable", "testdata", "testproc", keyProperties, properties, schema: "bimse");

			creator.DropSchema(true, true, true);
			creator.CreateSchema(true, true, true);
		}

		public class SomeClass
		{
			public int Id { get; set; }
			public string Text { get; set; }
		}
	}
}
using System.ComponentModel.DataAnnotations;
using Debaser.Attributes;
using NUnit.Framework;

namespace Debaser.Tests.Readme
{
	[TestFixture]
	public class SchemaSnips : FixtureBase
	{
		[Test]
		public void Checkperson()
		{
			var helper = new UpsertHelper<Person>(ConnectionString);
			helper.DropSchema(dropTable: true, dropProcedure: true, dropType: true);
			helper.CreateSchema();
		}

		private class Person
		{
			public Person(string ssn, string fullName)
			{
				Ssn = ssn;
				FullName = fullName;
			}

			[Key]
			public string Ssn { get; }

			public string FullName { get; }
		}

		[Test]
		public void CheckTenantPerson()
		{
			var helper = new UpsertHelper<TenantPerson>(ConnectionString);
			helper.DropSchema(dropTable: true, dropProcedure: true, dropType: true);
			helper.CreateSchema();
		}

		private class TenantPerson
		{
			public TenantPerson(string tenantId, string ssn, string fullName)
			{
				TenantId = tenantId;
				Ssn = ssn;
				FullName = fullName;
			}

			[Key]
			public string TenantId { get; }

			[Key]
			public string Ssn { get; }

			public string FullName { get; }
		}
	}
}
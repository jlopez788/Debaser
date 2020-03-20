using System.ComponentModel.DataAnnotations;
using NUnit.Framework;

namespace Debaser.Tests.Readme
{
	[TestFixture]
	public class SchemaSnips : FixtureBase
	{
		public class Person
		{
			public string FullName { get; }

			[Key]
			public string Ssn { get; }

			public Person(string ssn, string fullName)
			{
				Ssn = ssn;
				FullName = fullName;
			}
		}

		public class TenantPerson
		{
			public string FullName { get; }

			[Key]
			public string Ssn { get; }

			[Key]
			public string TenantId { get; }

			public TenantPerson(string tenantId, string ssn, string fullName)
			{
				TenantId = tenantId;
				Ssn = ssn;
				FullName = fullName;
			}
		}

		[Test]
		public void Checkperson()
		{
			var helper = new UpsertHelper<Person>(ConnectionString);
			helper.DropSchema(dropTable: true, dropProcedure: true, dropType: true);
			helper.CreateSchema();
		}

		[Test]
		public void CheckTenantPerson()
		{
			var helper = new UpsertHelper<TenantPerson>(ConnectionString);
			helper.DropSchema(dropTable: true, dropProcedure: true, dropType: true);
			helper.CreateSchema();
		}
	}
}
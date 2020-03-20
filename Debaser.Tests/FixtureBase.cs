using System;
using NUnit.Framework;

namespace Debaser.Tests {

	public abstract class FixtureBase {
		protected static string ConnectionString => Environment.GetEnvironmentVariable("testdb") ?? "server=(localdb)\\MSSQLLocalDB; database=debaser; trusted_connection=true";

		[SetUp]
		public void InnerSetUp() {
			SetUp();
		}

		protected virtual void SetUp() {
		}
	}
}
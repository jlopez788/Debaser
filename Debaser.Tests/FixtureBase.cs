using NUnit.Framework;
using System;

namespace Debaser.Tests
{
	public abstract class FixtureBase
	{
		private const int DatabaseAlreadyExists = 1801;

		protected static string ConnectionString => Environment.GetEnvironmentVariable("testdb") ?? "server=.; database=debaser_test; trusted_connection=true";

		[SetUp]
		public void InnerSetUp()
		{
			SetUp();
		}

		protected virtual void SetUp()
		{
		}
	}
}
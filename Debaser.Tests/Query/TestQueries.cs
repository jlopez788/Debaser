﻿using Debaser.Attributes;
using NUnit.Framework;
using System.Threading.Tasks;

namespace Debaser.Tests.Query
{
	[TestFixture]
	public class TestQueries : FixtureBase
	{
		private UpsertHelper<RowWithData> _upsertHelper;

		protected override void SetUp()
		{
			_upsertHelper = new UpsertHelper<RowWithData>(ConnectionString);

			_upsertHelper.DropSchema(dropTable: true, dropProcedure: true, dropType: true);
			_upsertHelper.CreateSchema();
		}

		[Test]
		public async Task CanQueryRows()
		{
			var rows = new[]
			{
				new RowWithData(1, "number1"),
				new RowWithData(2, "number2"),
				new RowWithData(3, "number3"),
				new RowWithData(4, "number4"),
				new RowWithData(5, "number5"),
			};

			await _upsertHelper.Upsert(rows);

			var results1 = await _upsertHelper.LoadWhere("[Data] = 'number4'");
			var results2 = await _upsertHelper.LoadWhere("[Data] = @data", new { data = "number4" });

			Assert.That(results1.Count, Is.EqualTo(1));
			Assert.That(results2.Count, Is.EqualTo(1));

			Assert.That(results1[0].Id, Is.EqualTo(4));
			Assert.That(results2[0].Id, Is.EqualTo(4));
		}

		private class RowWithData
		{
			public RowWithData(int id, string data)
			{
				Id = id;
				Data = data;
			}

			[DebaserKey]
			public int Id { get; }

			public string Data { get; }
		}
	}
}
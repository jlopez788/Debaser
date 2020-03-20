﻿using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

namespace Debaser.Tests
{
	[TestFixture]
	public class TestUpsertHelper : FixtureBase
	{
		private UpsertHelper<SimpleRow> _upsertHelper;

		protected override void SetUp()
		{
			_upsertHelper = new UpsertHelper<SimpleRow>(ConnectionString);

			_upsertHelper.DropSchema(dropTable: true, dropProcedure: true, dropType: true);
			_upsertHelper.CreateSchema();
		}

		[Test]
		public async Task CanRoundtripSingleRow()
		{
			await _upsertHelper.Modify(new[]
			{
				new SimpleRow {Id = 1, Text = "this is the first row"},
				new SimpleRow {Id = 2, Text = "this is the second row"},
			});

			var rows = _upsertHelper.LoadAll().OrderBy(r => r.Id).ToList();

			Assert.That(rows.Count, Is.EqualTo(2));
			Assert.That(rows.Select(r => r.Id), Is.EqualTo(new[] { 1, 2 }));
			Assert.That(rows.Select(r => r.Text), Is.EqualTo(new[] { "this is the first row", "this is the second row" }));
		}

		private class SimpleRow
		{
			public int Id { get; set; }

			public string Text { get; set; }
		}
	}
}
﻿using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

namespace Debaser.Tests.Corners
{
	[TestFixture]
	public class InsertEmptySequence : FixtureBase
	{
		private UpsertHelper<MinimalRow> _upserter;

		protected override void SetUp()
		{
			_upserter = new UpsertHelper<MinimalRow>(ConnectionString);
			_upserter.DropSchema(dropTable: true, dropProcedure: true, dropType: true);
			_upserter.CreateSchema();
		}

		[Test]
		public async Task DoesNotDieWhenUpsertingEmptySequence()
		{
			await _upserter.Modify(Enumerable.Empty<MinimalRow>());
		}

		[Test]
		public async Task DoesNotDieWhenUpsertingMinimalRows()
		{
			await _upserter.Modify(new[]
			{
				new MinimalRow(1),
				new MinimalRow(2),
				new MinimalRow(3),
				new MinimalRow(4),
			});

			var allRows = _upserter.LoadAll().OrderBy(r => r.Id).ToList();

			Assert.That(allRows.Select(r => r.Id), Is.EqualTo(new[] { 1, 2, 3, 4 }));
		}

		private class MinimalRow
		{
			public MinimalRow(int id)
			{
				Id = id;
			}

			public int Id { get; }
		}
	}
}
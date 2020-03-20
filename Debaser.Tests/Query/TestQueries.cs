using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using NUnit.Framework;

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
		public async Task QueryExpression()
		{
			var rows = new[]
			   {
				new RowWithData(1, "number1"),
				new RowWithData(2, "number2"),
				new RowWithData(3, "number3"),
				new RowWithData(4, "number4"),
				new RowWithData(5, "number5"),
			};

			var rowCount = await _upsertHelper.Modify(rows);
			Assert.AreEqual(5, rowCount);

			var array = new List<string> { "number1", "number2" };
			var data = await _upsertHelper.LoadAsync(r => r.Id == 3 || array.Contains(r.Data));

			Assert.AreEqual(3, data.Count);
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

			await _upsertHelper.Modify(rows);

			var results1 = await _upsertHelper.LoadAsync("[Data] = 'number4'");
			var results2 = await _upsertHelper.LoadAsync("[Data] = @data", new { data = "number4" });

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

			[Key]
			public int Id { get; }

			public string Data { get; }
		}
	}
}
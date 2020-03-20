using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Debaser.Tests.Bugs
{
	[TestFixture]
	public class TestBricData : FixtureBase
	{
		public class BricData
		{
			[Key]
			public string CellId { get; set; }

			public double GnsHstIndk2010 { get; set; }
			public float GnsPersIndkHigh2010 { get; set; }
		}

		private UpsertHelper<BricData> _upserter;

		[Test]
		public async Task CanWriteDoublesAndFloats()
		{
			var rowCount = await _upserter.Modify(new[] { new BricData { CellId = "hg03jg93", GnsHstIndk2010 = 24, GnsPersIndkHigh2010 = 3435 } });

			Assert.AreEqual(1, rowCount);
		}

		protected override void SetUp()
		{
			_upserter = new UpsertHelper<BricData>(ConnectionString);
			_upserter.DropSchema(dropTable: true, dropProcedure: true, dropType: true);
			_upserter.CreateSchema();
		}
	}
}
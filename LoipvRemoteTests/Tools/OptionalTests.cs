using LoipvRemote.Tools;
using NUnit.Framework;

namespace LoipvRemoteTests.Tools
{
	public class OptionalTests
	{
		[Test]
		public void MaybeReturnsEmptyListWhenGivenNullValue()
		{
			var sut = new OptionalValue<object>(null);
			Assert.That(sut, Is.Empty);
		}

		[Test]
		public void MaybeReturnsValueIfNotNull()
		{
			var expected = new object();
			var sut = new OptionalValue<object>(expected);
			Assert.That(sut, Has.Member(expected));
		}

	    [Test]
	    public void MaybeExtensionOfNullObjectIsntNull()
	    {
	        var sut = ((object) null).Maybe();
            Assert.That(sut, Is.Not.Null);
	    }

		[Test]
		public void OptionalValuesCompareByPresenceAndValue()
		{
			var empty = new OptionalValue<int>();
			var one = new OptionalValue<int>(1);
			var two = new OptionalValue<int>(2);

			Assert.That(empty < one, Is.True);
			Assert.That(one < two, Is.True);
			Assert.That(one <= new OptionalValue<int>(1), Is.True);
			Assert.That(two > one, Is.True);
		}

		[Test]
		public void OptionalValuesUseValueEquality()
		{
			Assert.That(new OptionalValue<string>("value"), Is.EqualTo(new OptionalValue<string>("value")));
			Assert.That(new OptionalValue<string>("value") == new OptionalValue<string>("value"), Is.True);
			Assert.That(new OptionalValue<string>("value") != new OptionalValue<string>("other"), Is.True);
		}
	}
}

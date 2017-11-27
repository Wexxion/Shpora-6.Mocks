using System.Collections.Generic;
using FakeItEasy;
using FluentAssertions;
using NUnit.Framework;

namespace MockFramework
{
    public class ThingCache
    {
        private readonly IDictionary<string, Thing> dictionary
            = new Dictionary<string, Thing>();

        private readonly IThingService thingService;

        public ThingCache(IThingService thingService)
        {
            this.thingService = thingService;
        }

        public Thing Get(string thingId)
        {
            if (dictionary.TryGetValue(thingId, out var thing))
                return thing;
            if (thingService.TryRead(thingId, out thing))
            {
                dictionary[thingId] = thing;
                return thing;
            }
            return null;
        }
    }

    [TestFixture]
    public class ThingCache_Should
    {
        private IThingService thingService;
        private ThingCache thingCache;

        private const string thingId1 = "TheDress";
        private Thing thing1 = new Thing(thingId1);

        private const string thingId2 = "CoolBoots";
        private Thing thing2 = new Thing(thingId2);

        private const string thingId3 = "Skirt";

        private Thing tempThing;

        [SetUp]
        public void SetUp()
        {
            thingService = A.Fake<IThingService>();
            A.CallTo(() => thingService.TryRead(thingId1, out thing1)).Returns(true);
            A.CallTo(() => thingService.TryRead(thingId2, out thing2)).Returns(true);
            A.CallTo(() => thingService.TryRead(thingId3, out tempThing)).Returns(false);
            thingCache = new ThingCache(thingService);
        }

        [Test]
        public void CallToServiceOnlyOnce()
        {
            thingCache.Get(thingId1);
            thingCache.Get(thingId1);
            A.CallTo(() => thingService.TryRead(A<string>.Ignored, out tempThing))
                .MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void ReturnSameObjectTwice()
        {
            var thingA = thingCache.Get(thingId1);
            var thingB = thingCache.Get(thingId1);
            
            thingA.Should().BeSameAs(thingB);
        }

        [Test]
        public void CallToServiceTwice_WhenTryingToGet2DifferentThings()
        {
            thingCache.Get(thingId1);
            thingCache.Get(thingId2);
            A.CallTo(() => thingService.TryRead(A<string>.Ignored, out tempThing))
                .MustHaveHappened(Repeated.Exactly.Twice);
        }

        [Test]
        public void ReturnsTwoDifferentObjects_WhenGotTwoDifferentCalls()
        {
            var thingA = thingCache.Get(thingId1);
            var thingB = thingCache.Get(thingId2);
            thingA.Should().NotBeSameAs(thingB);
        }

        [Test]
        public void ReturnsNull_WhenIdNotInService()
        {
            var thing = thingCache.Get(thingId3);
            thing.Should().Be(null);
            A.CallTo(() => thingService.TryRead(A<string>.Ignored, out tempThing))
                .MustHaveHappened(Repeated.Exactly.Once);
        }

        [Test]
        public void CallsServiceTwice_WhenIdNotInService()
        {
            thingCache.Get(thingId3);
            thingCache.Get(thingId3);
            A.CallTo(() => thingService.TryRead(A<string>.Ignored, out tempThing))
                .MustHaveHappened(Repeated.Exactly.Twice);
        }
    }
}
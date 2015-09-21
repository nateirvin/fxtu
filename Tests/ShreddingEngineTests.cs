using NUnit.Framework;
using XmlToTable.Core;

namespace Tests
{
    [TestFixture]
    public class ShreddingEngineTests
    {
        [Test]
        public void DefaultConstructorSetsCorrectlyIfNoConfigValuesPresent()
        {
            ShreddingEngine engine = new ShreddingEngine();
            Assert.AreEqual(ComponentState.Uninitialized, engine.EngineState);
        }
    }
}
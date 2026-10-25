using System;
using System.Reflection;
using NUnit.Framework;
namespace Group10.GuidedJourney.Tests
{
    public sealed class SpatialStabilizationTests
    {
        [TestCase("EnvelopeAndScale")]
        [TestCase("FurnitureSupport")]
        [TestCase("DoorsAndRecovery")]
        [TestCase("CoreCapsulePaths")]
        [TestCase("ExplicitContactReferences")]
        [TestCase("TaskAnchorsAndData")]
        [TestCase("ShoppingBagSizes")]
        public void ImportedFormalScenePreservesSpatialContract(string group)
        {
            var type=Type.GetType("Phase3SpatialVerification, Assembly-CSharp-Editor",true);
            var method=type.GetMethod("CheckGroup",BindingFlags.Public|BindingFlags.Static);
            var errors=(string[])method.Invoke(null,new object[]{group});
            Assert.That(errors,Is.Empty,string.Join(" | ",errors));
        }
    }
}
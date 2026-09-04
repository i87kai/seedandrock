using NUnit.Framework;
using UnityEngine;

namespace SeedAndRock.Tests
{
    /// <summary>Ensures the project-facing shader names resolve and compile in Unity's active URP renderer.</summary>
    public sealed class ShaderResolutionTests
    {
        [TestCase("SeedAndRock/Stylized Terrain")]
        [TestCase("SeedAndRock/Stylized Water")]
        [TestCase("SeedAndRock/Stylized Grass")]
        [TestCase("SeedAndRock/Stylized Environment")]
        public void ProjectShaderResolvesAndIsSupported(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null, "Shader.Find could not resolve " + shaderName);
            Assert.That(shader.isSupported, Is.True, shaderName + " is not supported by the active Unity renderer.");
        }
    }
}

using GameClient.Presentation.Board3D;
using NUnit.Framework;
using UnityEngine;

namespace GameClient.Tests.EditMode.Rendering
{
    public class RoundedTileMeshTests
    {
        [Test]
        public void Build_ProducesSlabWithRequestedExtents()
        {
            var mesh = RoundedTileMesh.Build(width: 1.0f, height: 1.3f, thickness: 0.18f,
                                             cornerRadius: 0.16f, cornerSegments: 6);

            Assert.Greater(mesh.triangles.Length, 0, "mesh should have triangles");
            Assert.AreEqual(1.0f, mesh.bounds.size.x, 0.001f);
            Assert.AreEqual(1.3f, mesh.bounds.size.y, 0.001f);
            Assert.AreEqual(0.18f, mesh.bounds.size.z, 0.01f);
        }

        [Test]
        public void Build_ClampsCornerRadiusToHalfShortSide()
        {
            // radius bigger than half the short side must not blow past the extents
            var mesh = RoundedTileMesh.Build(1.0f, 1.3f, 0.18f, cornerRadius: 5f, cornerSegments: 4);
            Assert.LessOrEqual(mesh.bounds.size.x, 1.001f);
            Assert.LessOrEqual(mesh.bounds.size.y, 1.301f);
        }
    }
}

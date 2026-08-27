using GameClient.Presentation.Board3D;
using NUnit.Framework;
using UnityEngine;

namespace GameClient.Tests.EditMode.Rendering
{
    public class TileFaceTextureTests
    {
        private static readonly Color Ivory = new Color(0.965f, 0.949f, 0.902f); // ~#F6F2E6
        private static readonly Color Jade  = new Color(0.184f, 0.541f, 0.329f); // ~#2F8A54

        [Test]
        public void Build_CentreIsIvory_NotJade()
        {
            var tex = TileFaceTexture.Build(128, Ivory, Ivory, Jade,
                framePadding: 0.10f, frameThickness: 0.03f, cornerRadius: 0.14f);
            var c = tex.GetPixel(64, 64);
            Assert.Less(Vector4.Distance((Vector4)c, (Vector4)Ivory), 0.08f, "centre should be ivory");
            Assert.Greater(Vector4.Distance((Vector4)c, (Vector4)Jade), 0.2f, "centre must not be jade");
        }

        [Test]
        public void Build_FrameRingContainsJade()
        {
            var tex = TileFaceTexture.Build(128, Ivory, Ivory, Jade,
                framePadding: 0.10f, frameThickness: 0.03f, cornerRadius: 0.14f);
            // walk a vertical line inward from the top edge; the frame band should
            // produce at least one strongly-jade pixel.
            bool foundJade = false;
            for (int y = 0; y < 64; y++)
            {
                var c = tex.GetPixel(64, y);
                if (Vector4.Distance((Vector4)c, (Vector4)Jade) < 0.08f) { foundJade = true; break; }
            }
            Assert.IsTrue(foundJade, "expected a jade frame pixel along the inset ring");
        }
    }
}

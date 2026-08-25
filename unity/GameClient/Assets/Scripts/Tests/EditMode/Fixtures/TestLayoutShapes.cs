using NUnit.Framework;
using System.Collections.Generic;
using GameDomain.Generation;
using GameDomain.Model;

namespace GameDomain.Tests.Fixtures
{
    public static class TestLayoutShapes
    {
        public static List<TileSlot> BuildLayeredRowShape(int[] rowLengthsByLayer) =>
            LayeredRowShapeBuilder.Build(rowLengthsByLayer);

        public static List<TileSlot> SmallShape() => BuildLayeredRowShape(new[] { 8 });
        public static List<TileSlot> MediumShape() => BuildLayeredRowShape(new[] { 12, 6 });
        public static List<TileSlot> LargeShape() => BuildLayeredRowShape(new[] { 20, 12, 6, 2 });
    }
}

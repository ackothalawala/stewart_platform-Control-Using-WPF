using System.Collections.Generic;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;

namespace stewart_platform.Visualization
{
    public class StewartVisualizer
    {
        private readonly TubeVisual3D baseVisual;
        private readonly TubeVisual3D platformVisual;
        private readonly IReadOnlyList<TubeVisual3D> hornVisuals;
        private readonly IReadOnlyList<TubeVisual3D> rodVisuals;

        public StewartVisualizer(TubeVisual3D baseVisual, TubeVisual3D platformVisual,
            IReadOnlyList<TubeVisual3D> hornVisuals, IReadOnlyList<TubeVisual3D> rodVisuals)
        {
            this.baseVisual = baseVisual;
            this.platformVisual = platformVisual;
            this.hornVisuals = hornVisuals;
            this.rodVisuals = rodVisuals;
        }

        public void Update(StewartPlatform platform)
        {
            var basePath = new Point3DCollection();
            foreach (var p in platform.BasePoints) basePath.Add(p);
            basePath.Add(platform.BasePoints[0]);
            baseVisual.Path = basePath;

            var platPath = new Point3DCollection();
            foreach (var p in platform.PlatformPoints) platPath.Add(p);
            platPath.Add(platform.PlatformPoints[0]);
            platformVisual.Path = platPath;

            for (int i = 0; i < 6; i++)
            {
                var hornPath = new Point3DCollection();
                hornPath.Add(platform.BasePoints[i]);
                hornPath.Add(platform.HornEndPoints[i]);
                hornVisuals[i].Path = hornPath;

                var rodPath = new Point3DCollection();
                rodPath.Add(platform.HornEndPoints[i]);
                rodPath.Add(platform.PlatformPoints[i]);
                rodVisuals[i].Path = rodPath;
            }
        }
    }
}

namespace Sarabande.Obstacles
{
    public interface IObstacle
    {
        public ObstacleData obstacleData { get; }

    }

#pragma warning disable CS0618
    public static class ObstacleEvents
    {
        public static void RegisterObstacle(this IObstacle obstacle) => NavigationEvents.Raise_StaticObstacleRegistry(obstacle.obstacleData);

    }
#pragma warning restore CS0618
}


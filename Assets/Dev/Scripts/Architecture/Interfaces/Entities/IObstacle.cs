namespace Sarabande.Obstacles
{
    public interface IObstacle
    {
        public ObstacleData obstacleData { get; }

        public void Build() => NavigationEvents.NotifyStaticObstacleRegistry(obstacleData);
    }
}


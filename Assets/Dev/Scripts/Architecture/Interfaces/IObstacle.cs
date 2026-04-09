using Sarabande.Levels;
using UnityEngine;

public interface IObstacle
{
    public ObstacleData obstacleData { get; }

    public void Build() => NavigationEvents.NotifyStaticObstacleRegistry(obstacleData);
}

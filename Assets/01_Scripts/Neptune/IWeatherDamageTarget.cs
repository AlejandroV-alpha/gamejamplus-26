using UnityEngine;

public interface IWeatherDamageTarget
{
    Transform WeatherTransform { get; }
    bool CanReceiveWeatherDamage { get; }
    double WeatherTime { get; }
    void ReceiveWeatherDamage(float amount);
}

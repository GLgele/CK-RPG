namespace CloudBuildPlugin.Common
{
    public interface IPolling
    {
        void SetFrequently(bool frequently);
        
        void TryPolling(double current);
    }

    public class Polling : IPolling
    {
        public const float INTERVAL_FREQUENTLY = 3f, INTERVAL_INFREQUENTLY = 60f;

        public bool isActive;
        float interval;
        double latestUpdateTime;
        public OnPolling pollingDelegate;

        public delegate void OnPolling();

        public Polling(OnPolling onPoling)
        {
            pollingDelegate = onPoling;
            SetFrequently(false);
        }

        public void SetInterval(float i)
        {
            interval = i;
        }

        public void TryPolling(double current)
        {
            if (isActive && current - latestUpdateTime > interval)
            {
                latestUpdateTime = current;
                pollingDelegate();
            }
        }

        public void SetFrequently(bool frequently)
        {
            SetInterval(frequently ? INTERVAL_FREQUENTLY : INTERVAL_INFREQUENTLY);
        }
    }
}
using System.Collections.Generic;

namespace CloudBuildPlugin.Common
{
    public enum UcbNotificationMessageTypes
    {
        Info,
        Warning,
        Error
    }
    
    public class UcbNotificationMessage
    {
        public UcbNotificationMessageTypes type { get; set; }
        public string content { get; set; }
        
        public UcbNotificationMessage(UcbNotificationMessageTypes t, string c)
        {
            type = t;
            content = c;
        }
    }
    
    public class MessageQueue
    {
        public const float DEQUEUE_INTERVAL = 1f;
        public static double latestDequeueTime = 0;
        
        private static Queue<UcbNotificationMessage> queue;

        public MessageQueue()
        {
            queue = new Queue<UcbNotificationMessage>();
        }

        public static void Enqueue(UcbNotificationMessage msg)
        {
            if (queue == null)
            {
                queue = new Queue<UcbNotificationMessage>();
            }

            lock (queue)
            {
                queue.Enqueue(msg);
            }
        }
        
        public static UcbNotificationMessage Dequeue()
        {
            if (queue != null && queue.Count > 0)
            {
                return queue.Dequeue();
            }
            return null;
        }
    }
}
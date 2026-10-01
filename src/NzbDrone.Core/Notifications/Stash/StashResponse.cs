using System.Collections.Generic;

namespace NzbDrone.Core.Notifications.Stash
{
    public class StashResponse<T>
    {
        public T Data { get; set; }
        public List<StashError> Errors { get; set; }
    }

    public class StashError
    {
        public string Message { get; set; }
    }

    public class StashStatusData
    {
        public StashSystemStatus SystemStatus { get; set; }
        public StashVersion Version { get; set; }
    }

    public class StashSystemStatus
    {
        public string Status { get; set; }
    }

    public class StashVersion
    {
        public string Version { get; set; }
    }
}

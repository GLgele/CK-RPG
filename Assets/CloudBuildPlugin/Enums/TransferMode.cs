using System;

namespace CloudBuildPlugin.Enums
{
    public enum TransferMode
    {
        COS,
        FTP,
        GIT,
        SVN,
        MERCURIAL
    }

    public static class TransferModeUtils
    {
        public static bool IsRepository(Object t)
        {
            return ((int) t >= (int) TransferMode.GIT) ;
        }

        public static bool IsGit(Object t)
        {
            return (int) t == (int) TransferMode.GIT;
        }
    }
}
